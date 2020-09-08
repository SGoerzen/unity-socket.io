#region License
/*
 * Encoder.cs
 *
 * The MIT License
 *
 * Copyright (c) 2014 Fabio Panettieri
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */
#endregion

using System;
using System.Text;

namespace SocketIO
{
	public class Encoder
	{
		public static string Encode(Packet packet)
		{
			try
			{
				var builder = new StringBuilder();

				// first is type
				builder.Append((int)packet.enginePacketType);
				if(!packet.enginePacketType.Equals(EnginePacketType.Message)){
					return builder.ToString();
				}

				builder.Append((int)packet.socketPacketType);

				// attachments if we have them
				if (packet.socketPacketType == SocketPacketType.BinaryEvent || packet.socketPacketType == SocketPacketType.BinaryAck) {
					builder.Append(packet.attachments);
					builder.Append('-');
				}

				// if we have a namespace other than '/'
				// we append it followed by a comma ','
				if (!string.IsNullOrEmpty(packet.nsp) && !packet.nsp.Equals("/")) {
					builder.Append(packet.nsp);
					builder.Append(',');
				}

				// immediately followed by the id
				if (packet.id > -1) {
					builder.Append(packet.id);
				}

				if (packet.json != null && !packet.json.ToString().Equals("null")) {
					builder.Append(packet.json.ToString());
				}

				return builder.ToString();
			
			} catch(Exception ex) {
				throw new SocketIOException("Packet encoding failed: " + packet, ex);
			}
		}
	}
}
