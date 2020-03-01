using System;
using System.IO;

namespace CSharp_zstd
{
	public class BitInputStream
	{
		public static bool IsEndOfStream(long startAddress, long currentAddress, int bitsConsumed)
		{
			return startAddress == currentAddress && bitsConsumed == 64;
		}

		public static long ReadTail(BinaryReader reader, long inputAddress, int inputSize)
		{
			reader.BaseStream.Seek(inputAddress, SeekOrigin.Begin);
			long bits = reader.ReadByte() & 0xFF;

			switch (inputSize) {
				case 7:
					reader.BaseStream.Seek(inputAddress + 6, SeekOrigin.Begin);
					bits |= (reader.ReadByte() & 0xFFL) << 48;
					goto case 6;
				case 6:
					reader.BaseStream.Seek(inputAddress + 5, SeekOrigin.Begin);
					bits |= (reader.ReadByte() & 0xFFL) << 40;
					goto case 5;
				case 5:
					reader.BaseStream.Seek(inputAddress + 4, SeekOrigin.Begin);
					bits |= (reader.ReadByte() & 0xFFL) << 32;
					goto case 4;
				case 4:
					reader.BaseStream.Seek(inputAddress + 3, SeekOrigin.Begin);
					bits |= (reader.ReadByte() & 0xFFL) << 24;
					goto case 3;
				case 3:
					reader.BaseStream.Seek(inputAddress + 2, SeekOrigin.Begin);
					bits |= (reader.ReadByte() & 0xFFL) << 16;
					goto case 2;
				case 2:
					reader.BaseStream.Seek(inputAddress + 1, SeekOrigin.Begin);
					bits |= (reader.ReadByte() & 0xFFL) << 8;
					break;
			}

			return bits;
		}

		/**
		* @return numberOfBits in the low order bits of a long
		*/
		public static long PeekBits(int bitsConsumed, long bitContainer, int numberOfBits)
		{
			return Util.UnsignedRightShift( Util.UnsignedRightShift((bitContainer << bitsConsumed), 1), (63 - numberOfBits));
		}

		/**
		* numberOfBits must be > 0
		*
		* @return numberOfBits in the low order bits of a long
		*/
		public static long PeekBitsFast(int bitsConsumed, long bitContainer, int numberOfBits)
		{
			return Util.UnsignedRightShift((bitContainer << bitsConsumed), (64 - numberOfBits));
		}
	}
}