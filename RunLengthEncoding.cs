using System;
using System.Collections.Generic;
using System.Linq;

namespace NetState
{
	public static class RunLengthEncoding
	{
		public static byte[] Encode(byte[] input)
		{
			var output = new List<byte>(input.Length);

			for (int i = 0; i < input.Length; i++)
			{
				var inByte = input[i];
				output.Add(inByte);

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
					output.Add(runLength);
					i += runLength;
				}
			}
			return output.ToArray();
		}

		public static byte[] Decode(byte[] input)
		{
			var output = new List<byte>(input.Length);

			for (int i = 0; i < input.Length; i++)
			{
				var inByte = input[i];
				output.Add(inByte);
				if (inByte == 0)
				{
					i++;
					byte runLength = input[i];
					for (int j = 0; j < runLength; j++)
					{
						output.Add(0);
					}
				}
			}
			return output.ToArray();
		}
	}
}
