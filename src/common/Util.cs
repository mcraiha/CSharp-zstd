using System;

namespace CSharp_zstd
{
	public static class Util
	{
		public static int HighestBit(int value)
		{
			return 31 - NumberOfLeadingZeros(value);
		}

		private static int NumberOfLeadingZeros(int i) 
		{
			if (i == 0)
			{
				return 32;
			}

			int n = 1;
			if (UnsignedRightShift(i, 16) == 0) { n += 16; i <<= 16; }
			if (UnsignedRightShift(i, 24) == 0) { n +=  8; i <<=  8; }
			if (UnsignedRightShift(i, 28) == 0) { n +=  4; i <<=  4; }
			if (UnsignedRightShift(i, 30) == 0) { n +=  2; i <<=  2; }
			n -= UnsignedRightShift(i, 31);
			
			return n;
		}

		public static bool IsPowerOf2(int value)
		{
			return (value & (value - 1)) == 0;
		}

		public static int Mask(int bits)
		{
			return (1 << bits) - 1;
		}

		public static long UnsignedRightShift(long value, int distance)
		{
			return (long)((ulong)value >> distance);
		}

		public static int UnsignedRightShift(int value, int distance)
		{
			return (int)((uint)value >> distance);
		}

		public static short UnsignedRightShift(short value, int distance)
		{
			return (short)((ushort)value >> distance);
		}

		public static void Verify(bool condition, long offset, string reason)
		{
			if (!condition) 
			{
				throw new ArgumentException($"{offset}, {reason}");
			}
		}

		public static void CheckArgument(bool condition, string reason)
		{
			if (!condition) 
			{
				throw new ArgumentException(reason);
			}
		}

		public static void CheckState(bool condition, string reason)
		{
			if (!condition) 
			{
				throw new InvalidOperationException(reason);
			}
		}

		public static void Fail(long offset, string reason)
		{
			throw new ArgumentException($"{offset}, {reason}");
		}

		/*public static int CycleLog(int hashLog, Strategy strategy)
		{
			int cycleLog = hashLog;
			if (strategy == Strategy.BTLAZY2 || strategy == Strategy.BTOPT || strategy == Strategy.BTULTRA) {
				cycleLog = hashLog - 1;
			}
			return cycleLog;
		}*/

		/*public static void put24BitLittleEndian(Object outputBase, long outputAddress, int value)
		{
			UNSAFE.putShort(outputBase, outputAddress, (short) value);
			UNSAFE.putByte(outputBase, outputAddress + Constants.SIZE_OF_SHORT, (byte) (value >>> Short.SIZE));
		}*/

		// provides the minimum logSize to safely represent a distribution
		public static int MinTableLog(int inputSize, int maxSymbolValue)
		{
			if (inputSize <= 1) {
				throw new ArgumentException("Not supported. RLE should be used instead"); // TODO
			}

			int minBitsSrc = HighestBit((inputSize - 1)) + 1;
			int minBitsSymbols = HighestBit(maxSymbolValue) + 2;
			return Math.Min(minBitsSrc, minBitsSymbols);
		}
	}
}