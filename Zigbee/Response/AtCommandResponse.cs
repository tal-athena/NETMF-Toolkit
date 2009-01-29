﻿/* 
 * AtCommandResponse.cs
 * 
 * Copyright (c) 2008, Michael Schwarz (http://www.schwarz-interactive.de)
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
using System;
using System.Text;
using MSchwarz.IO;

namespace MSchwarz.Net.XBee
{
    public class AtCommandResponse : XBeeResponse
    {
        private byte _frameID;
        private string _command;
        private byte _status;
        private byte[] _value;
		private IAtCommandData _data = null;

		public byte FrameID
		{
			get { return _frameID; }
		}

        public string Command
        {
            get { return _command; }
        }

        public AtCommandStatus Status
        {
			get { return (AtCommandStatus)_status; }
        }

        public byte[] Value
        {
            get { return _value; }
        }

		public IAtCommandData Data
		{
			get { return _data; }
		}

        public AtCommandResponse(short length, ByteReader br)
            : base(br)
        {
            _frameID = br.ReadByte();

#if(MF)
			_command = ByteUtil.GetString(br.ReadBytes(2));
#else
			_command = Encoding.ASCII.GetString(br.ReadBytes(2));
#endif

            _status = br.ReadByte();

			if (br.AvailableBytes > 0)
			{
				_value = br.ReadBytes(length - 5);

				switch (_command)
				{
					case "DB": _data = new ReceivedSignalStrengthData(); break;
					case "IS": _data = new ForceSampleData(); break;
					case "ND": _data = new NodeDiscoverData(); break;
					case "NI": _data = new NodeIdentifierData(); break;
					case "SM": _data = new SleepModeData(); break;
					case "SP": _data = new CyclicSleepPeriodData(); break;
					case "ST": _data = new TimeBeforeSleepData(); break;
					case "%V": _data = new SupplyVoltageData(); break;
					case "AP": _data = new ApiEnableData(); break;
				}

				if (_data != null && _value != null && _value.Length > 0)
				{
					_data.Fill(_value);
				}
			}
        }

		public override string ToString()
		{
			string s =
				"command " + _command + "\r\n" +
				"status  " + this.Status;

			if (_data != null)
				s += "\r\nvalue\r\n" + _data;
			else
				s += "\r\nvalue = " + ByteUtil.PrintBytes(_value);

			return s;
		}
    }
}
