﻿/* 
 * XBee.cs
 * 
 * Copyright (c) 2009, Michael Schwarz (http://www.schwarz-interactive.de)
 *
 * Permission is hereby granted, free of charge, to any person
 * obtaining a copy of this software and associated documentation
 * files (the "Software"), to deal in the Software without
 * restriction, including without limitation the rights to use,
 * copy, modify, merge, publish, distribute, sublicense, and/or
 * sell copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be
 * included in all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
 * OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
 * IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR
 * ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF
 * CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
 * CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 * 
 */
/*
 * MS	08-11-10	changed how data reading is working
 * BL   09-01-27    fixed MicroZigbee build
 * MS   09-02-06    fixed work item 3636 when first character is not the startbyte
 * PH   09-02-07    added several changes to enable stopping receive thread
 * MS   09-03-24    changed methods from SendCommand to Execute
 * MS   09-03-27    fixed if there are more than one API frame in received bytes
 *                  changed OnPacketReceived to FrameReceived
 *                  
 * 
 */
using System;
using System.Text;
using System.Threading;
using System.IO.Ports;
using System.IO;
using MFToolkit.IO;

namespace MFToolkit.Net.XBee
{
    /// <summary>
    /// Represents a XBee module communication class
    /// </summary>
    public class XBee : IDisposable
    {
        private string _port;
        private int _baudRate = 9600;
        private SerialPort _serialPort;
		private MemoryStream _readBuffer = new MemoryStream();
		private ApiType _apiType = ApiType.Unknown;
		
		private byte _frameID = 0x00;
		private bool _waitResponse = false;
		private XBeeResponse _receivedPacket = null;

        private Thread _thd;
        private bool _stopThd;

        #region Events

        [Obsolete("Use FrameReceivedEventHandler instead.", true)]
		public delegate void PacketReceivedHandler(XBee sender, XBeeResponse response);
        [Obsolete("Use FrameReceived instead.", true)]
        public event PacketReceivedHandler OnPacketReceived;

        public event FrameReceivedEventHandler FrameReceived;
        public event ModemStatusChangedEventHandler ModemStatusChanged;
        public event LogEventHandler LogEvent;

        #endregion

        #region Public Properties

        public ApiType ApiType
        {
            get { return _apiType; }
            protected set { _apiType = value; }
        }

        #endregion

        #region Constructor

        ~XBee()
        {
            Dispose();
        }

        public XBee(string port)
        {
            _port = port;
        }

        public XBee(string port, int baudRate)
            : this(port)
        {
            _baudRate = baudRate;
        }

		public XBee(string port, ApiType apiType)
			: this(port)
		{
			_apiType = apiType;
		}

		public XBee(string port, int baudRate, ApiType apiType)
			: this(port, baudRate)
		{
			_apiType = apiType;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Opens the connection to the XBee module with the specified port configuration
        /// </summary>
        /// <param name="port">The serial port (i.e. COM1)</param>
        /// <param name="baudRate">The baudrate to use</param>
        /// <returns></returns>
        public bool Open(string port, int baudRate)
        {
            _port = port;
            _baudRate = baudRate;

            return Open();
        }

        /// <summary>
        /// Opens the connection to the XBee module
        /// </summary>
        /// <returns></returns>
		public bool Open()
		{
			try
			{
				if (_serialPort == null)
					_serialPort = new SerialPort(_port, _baudRate);

				if (!_serialPort.IsOpen)
				{
					_serialPort.ReadTimeout = 2000;
					_serialPort.WriteTimeout = 2000;

					_serialPort.Open();
				}
			}
			catch (Exception ex)
			{
                OnLogEvent(LogEventType.ServerException, ex.ToString());
				return false;
			}

			if (_apiType == ApiType.Unknown)
			{
				#region Detecting API or transparent AT mode

				try
				{
                    WriteCommand("+++");
                    Thread.Sleep(1025);     // at least one second to wait for OK response

                    if (ReadResponse() == "OK")
                    {
                        _apiType = ApiType.Disabled;

                        // we need some msecs to wait before calling another EnterCommandMode
                        Thread.Sleep(1000);
                    }
				}
				catch (Exception)
				{
					// seems that we are using API

					_thd = new Thread(new ThreadStart(this.ReceiveData));
#if(!MF)
                    _thd.Name = "Receive Data Thread";
                    _thd.IsBackground = true;
#endif
					_thd.Start();

					AtCommandResponse at = Execute(new ApiEnable()) as AtCommandResponse;

					_apiType = (at.ParseValue() as ApiEnableData).ApiType;
				}

				#endregion
			}
			else if (_apiType == ApiType.Enabled || _apiType == ApiType.EnabledWithEscaped)
			{
				_thd = new Thread(new ThreadStart(this.ReceiveData));
#if(!MF)
                _thd.Name = "Receive Data Thread";
                _thd.IsBackground = true;
#endif
				_thd.Start();
			}

			if (_apiType == ApiType.Unknown)
				throw new NotSupportedException("The API type could not be read or is configured wrong.");

			return true;
        }

        /// <summary>
        /// Close the connection to the XBee module
        /// </summary>
        public void Close()
        {
            StopReceiveData();

            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
            }
        }

        /// <summary>
        /// Stops the receive thread
        /// </summary>
        public void StopReceiveData()
        {
            try
            {
                if (_thd != null)
                {
                    _stopThd = true;

                    // Block again until the DoWork thread finishes
                    _thd.Join(2000);
                    _thd.Abort();
                    _thd.Join(2000);
                }
            }
            catch (Exception ex)
            {
                OnLogEvent(LogEventType.ServerException, ex.ToString());
            }
            finally
            {
                _thd = null;
            }
        }

        /// <summary>
        /// Sends a XBeeRequest
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public bool ExecuteNonQuery(XBeeRequest request)
        {
            if (request is XBeeFrameRequest)
            {
                if (_frameID == byte.MaxValue)
                    _frameID = byte.MinValue;

                ((XBeeFrameRequest)request).FrameID = ++_frameID;
            }

            byte[] bytes;

            if (_apiType == ApiType.EnabledWithEscaped)
                bytes = request.GetEscapedApiPacket();
            else if (_apiType == ApiType.Enabled)
                bytes = request.GetApiPacket();
            else if (_apiType == ApiType.Disabled)
                bytes = request.GetAtPacket();
            else
                throw new NotSupportedException("This ApiType is not supported.");

#if(DEBUG && !MF && !WindowsCE)
            File.AppendAllText("log.txt", DateTime.Now.ToLongTimeString() + "\t>>\t" + ByteUtil.PrintBytes(bytes, false) + "\r\n");
#endif

            _serialPort.Write(bytes, 0, bytes.Length);

            return true;
        }

#if(!MF && !WindowsCE)

        public T Execute<T>(XBeeRequest request) where T : XBeeResponse
        {
            return (T)Execute(request);
        }

        public T Execute<T>(XBeeRequest request, int timeout) where T : XBeeResponse
        {
            return (T)Execute(request, timeout);
        }
#endif

        /// <summary>
        /// Sends a XBeeRequest and waits 1000 msec for the XBeeResponse
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        /// <exception cref="System.TimoutException">Throws an TimoutException when response could not be read.</exception>
        public XBeeResponse Execute(XBeeRequest request)
        {
            return Execute(request, 1000);
        }

        /// <summary>
        /// Sends a XBeeRequest and waits for the XBeeResponse for specified milliseconds
        /// </summary>
        /// <param name="request">The XBeeRequest.</param>
        /// <param name="timeout">Milliseconds to wait for the response.</param>
        /// <returns></returns>
        public XBeeResponse Execute(XBeeRequest request, int timeout)
        {
            _waitResponse = true;

            ExecuteNonQuery(request);

            if (_apiType == ApiType.Enabled || _apiType == ApiType.EnabledWithEscaped)
            {
                int c = 0;

                while (_waitResponse && ++c <= (timeout / 10))
                {
                    Thread.Sleep(10);
                }

                if (c > (timeout / 10))
                {
#if(MF)
				    throw new Exception("Could not receive response.");
#else
                    throw new TimeoutException("Could not receive response.");
#endif
                }

                if (_waitResponse)
                    return null;

                return _receivedPacket;
            }
            else if (_apiType == ApiType.Disabled)
            {
                if (ReadResponse() == "OK")
                    return null;

                // throw new NotImplementedException("This method is not yet implemented.");
            }

            throw new NotSupportedException("This ApiType is not supported.");
        }

        #endregion

        #region Private Methods

        private void ReceiveData()
		{
            try
            {
                int bytesToRead = _serialPort.BytesToRead;

                while (!_stopThd)
                {
                    if (bytesToRead == 0)
                    {
                        Thread.Sleep(20);
                    }
                    else
                    {

#if(DEBUG && !MF && !WindowsCE)
                        Console.WriteLine(bytesToRead + " bytes to read");
#endif

                        byte[] bytes = new byte[1024];	// TODO: what is the maximum size of Zigbee packets?

                        if (_serialPort == null || !_serialPort.IsOpen)
                        {
                            if (_serialPort == null)
                                _serialPort = new SerialPort(_port, _baudRate);

                            _serialPort.Open();

                            bytesToRead = _serialPort.BytesToRead;
                            _readBuffer.SetLength(0);
                            continue;
                        }

                        try
                        {
                            int bytesRead = _serialPort.Read(bytes, 0, bytesToRead);

                            for (int i = 0; i < bytesRead; i++)
                            {
                                if (_apiType == ApiType.EnabledWithEscaped && XBeePacket.IsSpecialByte(bytes[i]))
                                {
                                    if (bytes[i] == XBeePacket.PACKET_STARTBYTE)
                                    {
                                        _readBuffer.WriteByte(bytes[i]);
                                    }
                                    else if (bytes[i] == XBeePacket.PACKET_ESCAPE)
                                    {
                                        _readBuffer.WriteByte((byte)(0x20 ^ bytes[++i]));
                                    }
                                    else
                                        throw new Exception("This special byte should not appear.");
                                }
                                else
                                    _readBuffer.WriteByte(bytes[i]);
                            }
                           
                            bool startOK = false;
                            bool lengthAndCrcOK = false;

                            do
                            {
                                _readBuffer.Position = 0;

                                // startOK should be always true if there is at least on byte
                                // TODO: if not first byte is startbyte we need to search next startbyte
                                startOK = ((byte)_readBuffer.ReadByte() == XBeePacket.PACKET_STARTBYTE);
                                

                                lengthAndCrcOK = this.CheckLengthAndCrc();

                                bytes = _readBuffer.ToArray();
                                _readBuffer.SetLength(0);

                                ByteReader br = new ByteReader(bytes, ByteOrder.BigEndian);

#if(DEBUG && !MF && !WindowsCE)
                                File.AppendAllText("log.txt", DateTime.Now.ToLongTimeString() + "\t<<\t" + ByteUtil.PrintBytes(bytes, false) + "\r\n");
#endif

                                if (startOK && lengthAndCrcOK)
                                {
                                    byte startByte = br.ReadByte();     // start byte
                                    short length = br.ReadInt16();

                                    CheckFrame(length, br);

                                    if (br.AvailableBytes > 1)
                                    {
                                        br.ReadByte();  // checksum of current API message
                                        
                                        // ok, there are more bytes for an additional frame packet
                                        byte[] available = br.GetAvailableBytes();
                                        _readBuffer.Write(available, 0, available.Length);
                                    }
                                }
                                else
                                {
                                    _readBuffer.Write(bytes, 0, bytes.Length);
                                }
                            }
                            while (startOK & lengthAndCrcOK & (_readBuffer.Length > 4));
                        }
                        catch (Exception ex)
                        {
                            OnLogEvent(LogEventType.ServerException, ex.ToString());

                            _readBuffer.SetLength(0);

                            if (_serialPort != null && _serialPort.IsOpen)
                            {
                                bytesToRead = _serialPort.BytesToRead;
                            }
                        }
                    }

                    if (_serialPort != null && _serialPort.IsOpen)
                        bytesToRead = _serialPort.BytesToRead;
                }
            }
#if(!MF)
            catch (ThreadAbortException ex)
            {
                OnLogEvent(LogEventType.ServerException, ex.ToString());

#if(DEBUG && !MF && !WindowsCE)
                // Display a message to the console.
                Console.WriteLine("{0} : DisplayMessage thread terminating - {1}",
                    DateTime.Now.ToString("HH:mm:ss.ffff"),
                    (string)ex.ExceptionState);
#endif
            }
#else
            catch(Exception ex)
            {
                OnLogEvent(LogEventType.ServerException, ex.ToString());
            }
#endif
        }

        private bool CheckLengthAndCrc()
        {
            // can't be to short
            if (_readBuffer.Length < 4)
                return false;

            int length = (_readBuffer.ReadByte() << 8) + _readBuffer.ReadByte();

            // real length = start(1) + length.length(2) + length parameter + crc(1) 
            if ((length + 4) > _readBuffer.Length)
            {
                return false;
            }

            XBeeChecksum checksum = new XBeeChecksum();
            for (int i = 0; i < length; i++)
            {
                checksum.AddByte((byte)_readBuffer.ReadByte());
            }

            checksum.Compute();
            bool result = checksum.Verify((byte)_readBuffer.ReadByte());

            return result;
        }

		private void CheckFrame(short length, ByteReader br)
        {
            XBeeApiType apiId = (XBeeApiType)br.Peek();
            XBeeResponse res = null;

            switch (apiId)
            {
                case XBeeApiType.ATCommandResponse:
                    res = new AtCommandResponse(length, br);
                    break;
                case XBeeApiType.NodeIdentificationIndicator:
					res = new NodeIdentification(length, br);
                    break;
                case XBeeApiType.ZigBeeReceivePacket:
					res = new ZigBeeReceivePacket(length, br);
                    break;
                case XBeeApiType.XBeeSensorReadIndicator:
					res = new XBeeSensorRead(length, br);
                    break;
                case XBeeApiType.RemoteCommandResponse:
					res = new AtRemoteCommandResponse(length, br);
                    break;
				case XBeeApiType.ZigBeeIODataSampleRxIndicator:
					res = new ZigBeeIODataSample(length, br);
					break;
				case XBeeApiType.ZigBeeTransmitStatus:
					res = new ZigBeeTransmitStatus(length, br);
					break;
                case XBeeApiType.ModemStatus:
                    res = new ZigBeeModemStatus(length, br);

                    if (res != null)
                        OnModemStatusChanged((res as ZigBeeModemStatus).ModemStatus);

                    break;
				default:
					break;
            }

			if (res != null)
			{
				if (_waitResponse && res is AtCommandResponse && (res as AtCommandResponse).FrameID == _frameID)
				{
					_receivedPacket = res;
					_waitResponse = false;
				}

                OnFrameReceived(res);
			}
        }

        #region Event Handling

        private void OnFrameReceived(XBeeResponse response)
        {
            FrameReceivedEventHandler handler = FrameReceived;

            if (handler != null)
                handler(this, new FrameReceivedEventArgs(response));
        }

        private void OnModemStatusChanged(ZigBeeModemStatusType status)
        {
            ModemStatusChangedEventHandler handler = ModemStatusChanged;

            if (handler != null)
                handler(this, new ModemStatusChangedEventArgs(status));
        }

        private void OnLogEvent(LogEventType eventType, string message)
        {
            LogEventHandler handler = LogEvent;

            if (handler != null)
                handler(this, new LogEventArgs(eventType, message));
        }

        #endregion

        private string ReadTo(string value)
		{
			string textArrived = string.Empty;

			if (value == null)
				throw new ArgumentNullException();

			if (value.Length == 0)
				throw new ArgumentException();

			byte[] byteValue = Encoding.UTF8.GetBytes(value);
			int byteValueLen = byteValue.Length;

			bool flag = false;
			byte[] buffer = new byte[byteValueLen];

			do
			{
				int bytesRead;
				int bufferIndex = 0;
				Array.Clear(buffer, 0, buffer.Length);

				do
				{
					bytesRead = _serialPort.Read(buffer, bufferIndex, 1);
					bufferIndex += bytesRead;

					if (bytesRead <= 0)
					{
						return null;
					}
				}
				while ((bufferIndex < byteValueLen)
						&& (buffer[bufferIndex - 1] != byteValue[byteValueLen - 1]));

				char[] charData = Encoding.UTF8.GetChars(buffer);

				for (int i = 0; i < charData.Length; i++)
					textArrived += charData[i];

				flag = true;

				if (textArrived.Length > 0)
				{
					for (int i = 1; i <= value.Length; i++)
					{
						if (value[value.Length - i] != textArrived[textArrived.Length - i])
						{
							flag = false;
							break;
						}
					}
				}

			} while (!flag);

			if (textArrived.Length >= value.Length)
				textArrived = textArrived.Substring(0, textArrived.Length - value.Length);

			return textArrived;
		}

        protected void WriteCommand(string s)
        {
#if(DEBUG && !MF && !WindowsCE)
            Console.WriteLine(s);
#endif
            byte[] bytes = Encoding.UTF8.GetBytes(s);
            _serialPort.Write(bytes, 0, bytes.Length);
        }

		protected string ReadResponse()
		{
			string s = ReadTo("\r");

#if(DEBUG && !MF && !WindowsCE)
            Console.WriteLine(s);
#endif

			return s;
		}

		private bool StrEndWith(string data, string pattern)
		{
			int dataLen = data.Length;
			int patLen = pattern.Length;

			if (dataLen < patLen || data.Substring(dataLen - patLen) != pattern)
				return false;
			else
				return true;
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            Close();

            if(_serialPort != null)
            {
                _serialPort.Dispose();
                _serialPort = null;
            }
        }

        #endregion

        #region Obsolete Members

        [Obsolete("Use XBee.ExecuteNonQuery(XBeeRequest) instead.", true)]
        public bool SendPacket(XBeePacket packet)
        {
            throw new NotSupportedException("This method is not supported any more.");
        }

        [Obsolete("Use XBee.Execute(XBeeRequest) instead.", true)]
        public XBeeResponse SendCommand(AtCommand cmd)
        {
            throw new NotSupportedException("This method is not supported any more.");
        }

        #endregion
    }
}
