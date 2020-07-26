using System;

namespace CSharp_zstd
{
	public class FseCompressionTable
	{
		/*
		private readonly short[] nextState;
		private readonly int[] deltaNumberOfBits;
		private readonly int[] deltaFindState;

		private int log2Size;

		public FseCompressionTable(int maxTableLog, int maxSymbol)
		{
			nextState = new short[1 << maxTableLog];
			deltaNumberOfBits = new int[maxSymbol + 1];
			deltaFindState = new int[maxSymbol + 1];
		}

		public static FseCompressionTable NewInstance(short[] normalizedCounts, int maxSymbol, int tableLog)
		{
			FseCompressionTable result = new FseCompressionTable(tableLog, maxSymbol);
			result.Initialize(normalizedCounts, maxSymbol, tableLog);

			return result;
		}

		public void InitializeRleTable(int symbol)
		{
			log2Size = 0;

			nextState[0] = 0;
			nextState[1] = 0;

			deltaFindState[symbol] = 0;
			deltaNumberOfBits[symbol] = 0;
		}

		public void Initialize(short[] normalizedCounts, int maxSymbol, int tableLog)
		{
			int tableSize = 1 << tableLog;

			byte[] table = new byte[tableSize]; // TODO: allocate in workspace
			int highThreshold = tableSize - 1;

			// TODO: make sure FseCompressionTable has enough size
			log2Size = tableLog;

			// For explanations on how to distribute symbol values over the table:
			// http://fastcompression.blogspot.fr/2014/02/fse-distributing-symbol-values.html

			// symbol start positions
			int[] cumulative = new int[FiniteStateEntropy.MAX_SYMBOL + 2]; // TODO: allocate in workspace
			cumulative[0] = 0;
			for (int i = 1; i <= maxSymbol + 1; i++) 
			{
				if (normalizedCounts[i - 1] == -1) 
				{  
					// Low probability symbol
					cumulative[i] = cumulative[i - 1] + 1;
					table[highThreshold--] = (byte) (i - 1);
				}
				else 
				{
					cumulative[i] = cumulative[i - 1] + normalizedCounts[i - 1];
				}
			}
			cumulative[maxSymbol + 1] = tableSize + 1;

			// Spread symbols
			int position = SpreadSymbols(normalizedCounts, maxSymbol, tableSize, highThreshold, table);

			if (position != 0) 
			{
				throw new Exception("Spread symbols failed");
			}

			// Build table
			for (int i = 0; i < tableSize; i++) 
			{
				byte symbol = table[i];
				nextState[cumulative[symbol]++] = (short) (tableSize + i);  // TableU16 : sorted by symbol order; gives next state value 
			}

			// Build symbol transformation table
			int total = 0;
			for (int symbol = 0; symbol <= maxSymbol; symbol++) {
				switch (normalizedCounts[symbol]) {
					case 0:
						deltaNumberOfBits[symbol] = ((tableLog + 1) << 16) - tableSize;
						break;
					case -1:
					case 1:
						deltaNumberOfBits[symbol] = (tableLog << 16) - tableSize;
						deltaFindState[symbol] = total - 1;
						total++;
						break;
					default:
						int maxBitsOut = tableLog - Util.HighestBit(normalizedCounts[symbol] - 1);
						int minStatePlus = normalizedCounts[symbol] << maxBitsOut;
						deltaNumberOfBits[symbol] = (maxBitsOut << 16) - minStatePlus;
						deltaFindState[symbol] = total - normalizedCounts[symbol];
						total += normalizedCounts[symbol];
						break;
				}
			}
		}

		public int Begin(byte symbol)
		{
			int outputBits = Util.UnsignedRightShift(deltaNumberOfBits[symbol] + (1 << 15), 16);
			int baseAddress = Util.UnsignedRightShift((outputBits << 16) - deltaNumberOfBits[symbol], outputBits);
			return nextState[baseAddress + deltaFindState[symbol]];
		}

		public int Encode(BitOutputStream stream, int state, int symbol)
		{
			int outputBits = Util.UnsignedRightShift(state + deltaNumberOfBits[symbol], 16);
			stream.AddBits(state, outputBits);
			return nextState[Util.UnsignedRightShift(state, outputBits) + deltaFindState[symbol]];
		}

		public void Finish(BitOutputStream stream, int state)
		{
			stream.AddBits(state, log2Size);
			stream.Flush();
		}
		*/

		private static int CalculateStep(int tableSize)
		{
			return Util.UnsignedRightShift(tableSize, 1) + Util.UnsignedRightShift(tableSize, 3) + 3;
		}

		public static int SpreadSymbols(short[] normalizedCounters, int maxSymbolValue, int tableSize, int highThreshold, byte[] symbols)
		{
			int mask = tableSize - 1;
			int step = CalculateStep(tableSize);

			int position = 0;
			for (byte symbol = 0; symbol <= maxSymbolValue; symbol++) 
			{
				for (int i = 0; i < normalizedCounters[symbol]; i++) 
				{
					symbols[position] = symbol;
					do 
					{
						position = (position + step) & mask;
					}
					while (position > highThreshold);
				}
			}
			return position;
		}
	}
}