using System;
using System.IO;

namespace CSharp_zstd
{
	public class BitInputStreamLoader
	{
		private readonly BinaryReader input;
		private readonly long startAddress;
		private long bits;
		private long currentAddress;
		private int bitsConsumed;
		private bool overflow;

		public BitInputStreamLoader(BinaryReader input, long startAddress, long currentAddress, long bits, int bitsConsumed)
		{
			this.input = input;
			this.startAddress = startAddress;
			this.bits = bits;
			this.currentAddress = currentAddress;
			this.bitsConsumed = bitsConsumed;
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

		public bool IsOverflow()
		{
			return this.overflow;
		}

		public bool Load()
		{
			if (this.bitsConsumed > 64) 
			{
				this.overflow = true;
				return true;
			}
			else if (this.currentAddress == this.startAddress) 
			{
				return true;
			}

			int bytes = Util.UnsignedRightShift(this.bitsConsumed, 3); // divide by 8
			if (this.currentAddress >= this.startAddress + Constants.SIZE_OF_LONG) 
			{
				if (bytes > 0) 
				{
					this.currentAddress -= bytes;
					this.input.BaseStream.Seek(this.currentAddress, SeekOrigin.Begin);
					this.bits = this.input.ReadInt64();
				}
				this.bitsConsumed &= 0b111;
			}
			else if (currentAddress - bytes < startAddress) 
			{
				bytes = (int) (this.currentAddress - this.startAddress);
				this.currentAddress = this.startAddress;
				this.bitsConsumed -= bytes * Constants.SIZE_OF_LONG;
				this.input.BaseStream.Seek(this.startAddress, SeekOrigin.Begin);
				this.bits = this.input.ReadInt64();
				return true;
			}
			else 
			{
				this.currentAddress -= bytes;
				this.bitsConsumed -= bytes * Constants.SIZE_OF_LONG;
				this.input.BaseStream.Seek(this.currentAddress, SeekOrigin.Begin);
				this.bits = this.input.ReadInt64();
			}

			return false;
		}
	}
}