/* 
 * ZigbeeReceivePacket.cs
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

namespace MSchwarz.Net.Zigbee
{
	public class ZigbeeReceivePacket : XBeeResponse
	{
		private ushort _address16;
		private ulong _address64;
		private byte _options;
		private byte[] _rfdata;

		public ZigbeeReceivePacket(short length, ByteReader br)
			: base(br)
		{
			_address64 = br.ReadUInt64();
			_address16 = br.ReadUInt16();
			_options = br.ReadByte();

			_rfdata = br.ReadBytes(length - 12);
		}

		public override string ToString()
		{
			string s = "";

			s += "\taddress64 = " + _address64 + "\r\n";
			s += "\taddress16 = " + _address16 + "\r\n";
			s += "\toptions   = " + _options.ToString("X2") + "\r\n";
			s += "\tvalue     = " + ByteUtil.PrintBytes(_rfdata);

			return s;
		}
	}
}