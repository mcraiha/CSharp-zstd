using System;
using System.IO;

namespace CSharp_zstd
{
	public class FseTableReader
	{
		private readonly short[] nextSymbol = new short[FiniteStateEntropy.MAX_SYMBOL + 1];
		private readonly short[] normalizedCounters = new short[FiniteStateEntropy.MAX_SYMBOL + 1];

		public int ReadFseTable(FiniteStateEntropyTable table, BinaryReader inputReader, long inputAddress, long inputLimit, int maxSymbol, int maxTableLog)
		{
			// read table headers
			long input = inputAddress;
			Util.Verify(inputLimit - inputAddress >= 4, input, "Not enough input bytes");

			int threshold;
			int symbolNumber = 0;
			bool previousIsZero = false;

			inputReader.BaseStream.Seek(input, SeekOrigin.Begin);
			int bitStream = inputReader.ReadInt32();

			int tableLog = (bitStream & 0xF) + FiniteStateEntropy.MIN_TABLE_LOG;

			int numberOfBits = tableLog + 1;
			bitStream = Util.UnsignedRightShift(bitStream, 4);
			int bitCount = 4;

			Util.Verify(tableLog <= maxTableLog, input, "FSE table size exceeds maximum allowed size");

			int remaining = (1 << tableLog) + 1;
			threshold = 1 << tableLog;

			while (remaining > 1 && symbolNumber <= maxSymbol) 
			{
				if (previousIsZero) 
				{
					int n0 = symbolNumber;
					while ((bitStream & 0xFFFF) == 0xFFFF) 
					{
						n0 += 24;
						if (input < inputLimit - 5) 
						{
							input += 2;
							inputReader.BaseStream.Seek(input, SeekOrigin.Begin);
							bitStream = Util.UnsignedRightShift(inputReader.ReadInt32(), bitCount);
						}
						else 
						{
							// end of bit stream
							bitStream = Util.UnsignedRightShift(bitStream, 16);
							bitCount += 16;
						}
					}
					while ((bitStream & 3) == 3) 
					{
						n0 += 3;
						bitStream = Util.UnsignedRightShift(bitStream, 2);
						bitCount += 2;
					}
					n0 += bitStream & 3;
					bitCount += 2;

					Util.Verify(n0 <= maxSymbol, input, "Symbol larger than max value");

					while (symbolNumber < n0) 
					{
						normalizedCounters[symbolNumber++] = 0;
					}

					if ((input <= inputLimit - 7) || (input + Util.UnsignedRightShift(bitCount, 3) <= inputLimit - 4)) 
					{
						input += Util.UnsignedRightShift(bitCount, 3);
						bitCount &= 7;
						inputReader.BaseStream.Seek(input, SeekOrigin.Begin);
						bitStream = Util.UnsignedRightShift(inputReader.ReadInt32(), bitCount);
					}
					else 
					{
						bitStream = Util.UnsignedRightShift(bitStream, 2);
					}
				}

				short max = (short) ((2 * threshold - 1) - remaining);
				short count;

				if ((bitStream & (threshold - 1)) < max) 
				{
					count = (short) (bitStream & (threshold - 1));
					bitCount += numberOfBits - 1;
				}
				else 
				{
					count = (short) (bitStream & (2 * threshold - 1));
					if (count >= threshold) 
					{
						count -= max;
					}
					bitCount += numberOfBits;
				}
				count--;  // extra accuracy

				remaining -= Math.Abs(count);
				normalizedCounters[symbolNumber++] = count;
				previousIsZero = count == 0;
				while (remaining < threshold) 
				{
					numberOfBits--;
					threshold = Util.UnsignedRightShift(threshold, 1);
				}

				if ((input <= inputLimit - 7) || (input + (bitCount >> 3) <= inputLimit - 4)) 
				{
					input += Util.UnsignedRightShift(bitCount, 3);
					bitCount &= 7;
				}
				else 
				{
					bitCount -= (int) (8 * (inputLimit - 4 - input));
					input = inputLimit - 4;
				}
				inputReader.BaseStream.Seek(input, SeekOrigin.Begin);
				bitStream = Util.UnsignedRightShift(inputReader.ReadInt32(), (bitCount & 31));
			}

			Util.Verify(remaining == 1 && bitCount <= 32, input, "Input is corrupted");

			maxSymbol = symbolNumber - 1;
			Util.Verify(maxSymbol <= FiniteStateEntropy.MAX_SYMBOL, input, "Max symbol value too large (too many symbols for FSE)");

			input += (bitCount + 7) >> 3;

			// populate decoding table
			int symbolCount = maxSymbol + 1;
			int tableSize = 1 << tableLog;
			int highThreshold = tableSize - 1;

			table.log2Size = tableLog;

			for (byte symbol = 0; symbol < symbolCount; symbol++) {
				if (normalizedCounters[symbol] == -1) {
					table.symbol[highThreshold--] = symbol;
					nextSymbol[symbol] = 1;
				}
				else {
					nextSymbol[symbol] = normalizedCounters[symbol];
				}
			}

			int position = FseCompressionTable.SpreadSymbols(normalizedCounters, maxSymbol, tableSize, highThreshold, table.symbol);

			// position must reach all cells once, otherwise normalizedCounter is incorrect
			Util.Verify(position == 0, input, "Input is corrupted");

			for (int i = 0; i < tableSize; i++) 
			{
				byte symbol = table.symbol[i];
				short nextState = nextSymbol[symbol]++;
				table.numberOfBits[i] = (byte) (tableLog - Util.HighestBit(nextState));
				table.newState[i] = (short) ((nextState << table.numberOfBits[i]) - tableSize);
			}

			return (int) (input - inputAddress);
		}

		public static void InitializeRleTable(FiniteStateEntropyTable table, byte value)
		{
			table.log2Size = 0;
			table.symbol[0] = value;
			table.newState[0] = 0;
			table.numberOfBits[0] = 0;
		}
	}
}