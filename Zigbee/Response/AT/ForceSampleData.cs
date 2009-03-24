﻿/* 
 * ForceSampleData.cs
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
 * MS   09-02-06    fixed work item 3711
 * 
 * 
 */
using System;
using System.Text;
using MFToolkit.IO;

namespace MFToolkit.Net.XBee
{
    /// <summary>
    /// Represents a force sample command response structure
    /// </summary>
	public class ForceSampleData : IAtCommandData
	{
		private byte _numSamples;
		private byte _digitalChannelMask1;
		private byte _digitalChannelMask2;
		private byte _analogChannelMask;
		private byte _digital1;
		private byte _digital2;
		private ushort _AD0;
		private ushort _AD1;
		private ushort _AD2;
		private ushort _AD3;
		private ushort _supplyVoltage;

        #region Public Properties

        public byte NumSamples
        {
            get { return _numSamples; }
        }

        // ...

        public ushort AD0
        {
            get { return _AD0; }
        }

        public ushort AD1
        {
            get { return _AD1; }
        }

        public ushort AD2
        {
            get { return _AD2; }
        }

        public ushort AD3
        {
            get { return _AD3; }
        }

        // ...

        #endregion

        public void ReadBytes(ByteReader br)
		{
			_numSamples = br.ReadByte();
			_digitalChannelMask1 = br.ReadByte();
			_digitalChannelMask2 = br.ReadByte();
			_analogChannelMask = br.ReadByte();

			if (_digitalChannelMask1 != 0x00 || _digitalChannelMask2 != 0x00)
			{
				_digital1 = br.ReadByte();
				_digital2 = br.ReadByte();
			}

			if (_analogChannelMask != 0x00)
			{
				if ((_analogChannelMask & 0x01) == 0x01) _AD0 = br.ReadUInt16();
				if ((_analogChannelMask & 0x02) == 0x02) _AD1 = br.ReadUInt16();
				if ((_analogChannelMask & 0x04) == 0x04) _AD2 = br.ReadUInt16();
				if ((_analogChannelMask & 0x08) == 0x08) _AD3 = br.ReadUInt16();
                if ((_analogChannelMask & 0x80) == 0x80) _supplyVoltage = br.ReadUInt16();
			}
		}

		public override string ToString()
		{
			string s = "";

			if (_digitalChannelMask1 != 0x00 || _digitalChannelMask2 != 0x00)
			{
				s += "D1  = " + _digital1 + "\r\n";
				s += "D2  = " + _digital2 + "\r\n";
			}

			if ((_analogChannelMask & 0x01) == 0x01) s += "AD0 = " + _AD0 + "\r\n";
			if ((_analogChannelMask & 0x02) == 0x02) s += "AD1 = " + _AD1 + "\r\n";
			if ((_analogChannelMask & 0x04) == 0x04) s += "AD2 = " + _AD2 + "\r\n";
			if ((_analogChannelMask & 0x08) == 0x08) s += "AD3 = " + _AD3 + "\r\n";
			if ((_analogChannelMask & 0x80) == 0x80) s += "supplyVoltage = " + _supplyVoltage;

#if(!MF && !WindowsCE && DEBUG)
			double mVanalog = (((float)_AD2) / 1023.0) * 1200.0;
			double temp_C = (mVanalog - 500.0) / 10.0 - 4.0;
			double lux = (((float)_AD1) / 1023.0) * 1200.0;

			mVanalog = (((float)_AD3) / 1023.0) * 1200.0;
			double hum = ((mVanalog * (108.2 / 33.2)) - 0.16) / (5 * 0.0062 * 1000.0);

			s += "\r\ntemperature = " + temp_C + " °C\r\n";
			s += "light = " + lux + " lux\r\n";
			s += "humidity = " + hum;
#endif
			return s;
		}
	}
}
