using System;
using System.IO;

namespace CSharp_zstd
{

	public class BitInputStreamInitializer
	{
		private readonly BinaryReader input;
		private readonly long startAddress;
		private readonly long endAddress;
		private long bits;
		private long currentAddress;
		private int bitsConsumed;

		public BitInputStreamInitializer(BinaryReader input, long startAddress, long endAddress)
		{
			this.input = input;
			this.startAddress = startAddress;
			this.endAddress = endAddress;
		}

		public long GetBits()
		{
			return this.bits;
		}

		public long GetCurrentAddress()
		{
			return this.currentAddress;
		}

		public int GetBitsConsumed()
		{
			return this.bitsConsumed;
		}

		public void Initialize()
		{
			Util.Verify(endAddress - startAddress >= 1, startAddress, "Bitstream is empty");

			this.input.BaseStream.Seek(endAddress - 1, SeekOrigin.Begin);
			int lastByte = this.input.ReadByte() & 0xFF;
			Util.Verify(lastByte != 0, endAddress, "Bitstream end mark not present");

			bitsConsumed = Constants.SIZE_OF_LONG - Util.HighestBit(lastByte);

			int inputSize = (int) (endAddress - startAddress);

			if (inputSize >= Constants.SIZE_OF_LONG) 
			{  /* normal case */
				this.currentAddress = this.endAddress - Constants.SIZE_OF_LONG;
				this.input.BaseStream.Seek(this.currentAddress, SeekOrigin.Begin);
				this.bits = this.input.ReadInt64();
			}
			else 
			{
				this.currentAddress = this.startAddress;
				bits = BitInputStream.ReadTail(this.input, this.startAddress, inputSize);

				bitsConsumed += (Constants.SIZE_OF_LONG - inputSize) * 8;
			}
		}
	}
}