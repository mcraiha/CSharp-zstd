using System;
using System.IO;

namespace CSharp_zstd
{
	public class ZstdDecompressor
	{
		private readonly ZstdFrameDecompressor decompressor = new ZstdFrameDecompressor();

		public int Decompress(byte[] input, int inputOffset, int inputLength, byte[] output, int outputOffset, int maxOutputLength)
		{
			long inputAddress = 0 + inputOffset;
			long inputLimit = inputAddress + inputLength;
			long outputAddress = 0 + outputOffset;
			long outputLimit = outputAddress + maxOutputLength;

			return decompressor.Decompress(new BinaryReader( new MemoryStream(input)), inputAddress, inputLimit, new BinaryWriter(new MemoryStream(output)), outputAddress, outputLimit);
		}
	}
}