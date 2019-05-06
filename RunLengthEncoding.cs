using System;
using System.Collections.Generic;
using System.Linq;

namespace NetState
{
	public static class RunLengthEncoding
	{
		private static List<byte> byteList = new List<byte>(1024);
		public static byte[] Encode(byte[] input)
		{
			for (int i = 0; i < input.Length; i++)
			{
				var inByte = input[i];
				byteList.Add(inByte);

				if (inByte == 0)
				{
					byte runLength = 0;
					while (runLength <= 255)
					{
						var index = i+1+runLength;
						if (index >= input.Length)
						{
							break;
						}
						if (input[index] != 0)
						{
							break;
						}
						runLength++;
					}
					byteList.Add(runLength);
					i += runLength;
				}
			}
			var output = byteList.ToArray();
			byteList.Clear();
			return output;
		}

		public static byte[] Decode(byte[] input)
		{
			for (int i = 0; i < input.Length; i++)
			{
				var inByte = input[i];
				byteList.Add(inByte);
				if (inByte == 0)
				{
					i++;
					byte runLength = input[i];
					for (int j = 0; j < runLength; j++)
					{
						byteList.Add(0);
					}
				}
			}
			var output = byteList.ToArray();
			byteList.Clear();
			return output;
		}
	}
}
