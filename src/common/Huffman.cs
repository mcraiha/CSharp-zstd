using System;
using System.IO;

namespace CSharp_zstd
{
	public class Huffman
	{
		public const int MAX_SYMBOL = 255;
		public const int MAX_SYMBOL_COUNT = MAX_SYMBOL + 1;

		public const int MAX_TABLE_LOG = 12;
		public const int MIN_TABLE_LOG = 5;
		public const int MAX_FSE_TABLE_LOG = 6;

		// stats
		private readonly byte[] weights = new byte[MAX_SYMBOL + 1];
		private readonly int[] ranks = new int[MAX_TABLE_LOG + 1];

		// table
		private int tableLog = -1;
		private readonly byte[] symbols = new byte[1 << MAX_TABLE_LOG];
		private readonly byte[] numbersOfBits = new byte[1 << MAX_TABLE_LOG];

		private readonly FseTableReader reader = new FseTableReader();
   		private readonly FiniteStateEntropyTable fseTable = new FiniteStateEntropyTable(MAX_FSE_TABLE_LOG);

		public bool IsLoaded()
		{
			return this.tableLog != -1;
		}

		public int ReadTable(BinaryReader inputReader, long inputAddress, int size)
		{
			Array.Clear(ranks, 0, ranks.Length);
			long input = inputAddress;

			// read table header
			Util.Verify(size > 0, input, "Not enough input bytes");
			
			inputReader.BaseStream.Seek(input++, SeekOrigin.Begin);
			int inputSize = inputReader.ReadByte() & 0xFF;

			int outputSize;
			if (inputSize >= 128) 
			{
				outputSize = inputSize - 127;
				inputSize = ((outputSize + 1) / 2);

				Util.Verify(inputSize + 1 <= size, input, "Not enough input bytes");
				Util.Verify(outputSize <= MAX_SYMBOL + 1, input, "Input is corrupted");

				for (int i = 0; i < outputSize; i += 2) 
				{
					inputReader.BaseStream.Seek(input + i / 2, SeekOrigin.Begin);
					int value = inputReader.ReadByte() & 0xFF;
					weights[i] = (byte) Util.UnsignedRightShift(value, 4);
					weights[i + 1] = (byte) (value & 0b1111);
				}
			}
			else 
			{
				Util.Verify(inputSize + 1 <= size, input, "Not enough input bytes");

				long inputLimit = input + inputSize;
				input += reader.ReadFseTable(fseTable, inputReader, input, inputLimit, FiniteStateEntropy.MAX_SYMBOL, MAX_FSE_TABLE_LOG);
				outputSize = FiniteStateEntropy.Decompress(fseTable, inputReader, input, inputLimit, weights);
			}

			int totalWeight = 0;
			for (int i = 0; i < outputSize; i++) 
			{
				ranks[weights[i]]++;
				totalWeight += (1 << weights[i]) >> 1;   // TODO same as 1 << (weights[n] - 1)?
			}
			Util.Verify(totalWeight != 0, input, "Input is corrupted");

			tableLog = Util.HighestBit(totalWeight) + 1;
			Util.Verify(tableLog <= MAX_TABLE_LOG, input, "Input is corrupted");

			int total = 1 << tableLog;
			int rest = total - totalWeight;
			Util.Verify(Util.IsPowerOf2(rest), input, "Input is corrupted");

			int lastWeight = Util.HighestBit(rest) + 1;

			weights[outputSize] = (byte) lastWeight;
			ranks[lastWeight]++;

			int numberOfSymbols = outputSize + 1;

			// populate table
			int nextRankStart = 0;
			for (int i = 1; i < tableLog + 1; ++i) 
			{
				int current = nextRankStart;
				nextRankStart += ranks[i] << (i - 1);
				ranks[i] = current;
			}

			for (int n = 0; n < numberOfSymbols; n++) 
			{
				int weight = weights[n];
				int length = (1 << weight) >> 1;  // TODO: 1 << (weight - 1) ??

				byte symbol = (byte) n;
				byte numberOfBits = (byte) (tableLog + 1 - weight);
				for (int i = ranks[weight]; i < ranks[weight] + length; i++) 
				{
					symbols[i] = symbol;
					numbersOfBits[i] = numberOfBits;
				}
				ranks[weight] += length;
			}

			Util.Verify(ranks[1] >= 2 && (ranks[1] & 1) == 0, input, "Input is corrupted");

			return inputSize + 1;
		}

		public void DecodeSingleStream(BinaryReader inputReader, long inputAddress, long inputLimit, BinaryWriter outputWriter, long outputAddress, long outputLimit)
		{
			BitInputStreamInitializer initializer = new BitInputStreamInitializer(inputReader, inputAddress, inputLimit);
			initializer.Initialize();

			long bits = initializer.GetBits();
			int bitsConsumed = initializer.GetBitsConsumed();
			long currentAddress = initializer.GetCurrentAddress();

			int tableLog = this.tableLog;
			byte[] numbersOfBits = this.numbersOfBits;
			byte[] symbols = this.symbols;

			// 4 symbols at a time
			long output = outputAddress;
			long fastOutputLimit = outputLimit - 4;
			while (output < fastOutputLimit) 
			{
				BitInputStreamLoader loader = new BitInputStreamLoader(inputReader, inputAddress, currentAddress, bits, bitsConsumed);
				bool done = loader.Load();
				bits = loader.GetBits();
				bitsConsumed = loader.GetBitsConsumed();
				currentAddress = loader.GetCurrentAddress();
				if (done) 
				{
					break;
				}

				bitsConsumed = DecodeSymbol(outputWriter, output, bits, bitsConsumed, tableLog, numbersOfBits, symbols);
				bitsConsumed = DecodeSymbol(outputWriter, output + 1, bits, bitsConsumed, tableLog, numbersOfBits, symbols);
				bitsConsumed = DecodeSymbol(outputWriter, output + 2, bits, bitsConsumed, tableLog, numbersOfBits, symbols);
				bitsConsumed = DecodeSymbol(outputWriter, output + 3, bits, bitsConsumed, tableLog, numbersOfBits, symbols);
				output += Constants.SIZE_OF_INT;
			}

			DecodeTail(inputReader, inputAddress, currentAddress, bitsConsumed, bits, outputWriter, output, outputLimit);
		}

		public void Decode4Streams(BinaryReader inputReader, long inputAddress, long inputLimit, BinaryWriter outputWriter, long outputAddress, long outputLimit)
		{
			Util.Verify(inputLimit - inputAddress >= 10, inputAddress, "Input is corrupted"); // jump table + 1 byte per stream

			long start1 = inputAddress + 3 * Constants.SIZE_OF_SHORT; // for the shorts we read below

			inputReader.BaseStream.Seek(inputAddress, SeekOrigin.Begin);
			long start2 = start1 + (inputReader.ReadInt16() & 0xFFFF);

			inputReader.BaseStream.Seek(inputAddress + 2, SeekOrigin.Begin);
			long start3 = start2 + (inputReader.ReadInt16() & 0xFFFF);

			inputReader.BaseStream.Seek(inputAddress + 4, SeekOrigin.Begin);
			long start4 = start3 + (inputReader.ReadInt16() & 0xFFFF);

			BitInputStreamInitializer initializer = new BitInputStreamInitializer(inputReader, start1, start2);
			initializer.Initialize();
			int stream1bitsConsumed = initializer.GetBitsConsumed();
			long stream1currentAddress = initializer.GetCurrentAddress();
			long stream1bits = initializer.GetBits();

			initializer = new BitInputStreamInitializer(inputReader, start2, start3);
			initializer.Initialize();
			int stream2bitsConsumed = initializer.GetBitsConsumed();
			long stream2currentAddress = initializer.GetCurrentAddress();
			long stream2bits = initializer.GetBits();

			initializer = new BitInputStreamInitializer(inputReader, start3, start4);
			initializer.Initialize();
			int stream3bitsConsumed = initializer.GetBitsConsumed();
			long stream3currentAddress = initializer.GetCurrentAddress();
			long stream3bits = initializer.GetBits();

			initializer = new BitInputStreamInitializer(inputReader, start4, inputLimit);
			initializer.Initialize();
			int stream4bitsConsumed = initializer.GetBitsConsumed();
			long stream4currentAddress = initializer.GetCurrentAddress();
			long stream4bits = initializer.GetBits();

			int segmentSize = (int) ((outputLimit - outputAddress + 3) / 4);

			long outputStart2 = outputAddress + segmentSize;
			long outputStart3 = outputStart2 + segmentSize;
			long outputStart4 = outputStart3 + segmentSize;

			long output1 = outputAddress;
			long output2 = outputStart2;
			long output3 = outputStart3;
			long output4 = outputStart4;

			long fastOutputLimit = outputLimit - 7;
			int tableLog = this.tableLog;
			byte[] numbersOfBits = this.numbersOfBits;
			byte[] symbols = this.symbols;

			while (output4 < fastOutputLimit) 
			{
				stream1bitsConsumed = DecodeSymbol(outputWriter, output1, stream1bits, stream1bitsConsumed, tableLog, numbersOfBits, symbols);
				stream2bitsConsumed = DecodeSymbol(outputWriter, output2, stream2bits, stream2bitsConsumed, tableLog, numbersOfBits, symbols);
				stream3bitsConsumed = DecodeSymbol(outputWriter, output3, stream3bits, stream3bitsConsumed, tableLog, numbersOfBits, symbols);
				stream4bitsConsumed = DecodeSymbol(outputWriter, output4, stream4bits, stream4bitsConsumed, tableLog, numbersOfBits, symbols);

				stream1bitsConsumed = DecodeSymbol(outputWriter, output1 + 1, stream1bits, stream1bitsConsumed, tableLog, numbersOfBits, symbols);
				stream2bitsConsumed = DecodeSymbol(outputWriter, output2 + 1, stream2bits, stream2bitsConsumed, tableLog, numbersOfBits, symbols);
				stream3bitsConsumed = DecodeSymbol(outputWriter, output3 + 1, stream3bits, stream3bitsConsumed, tableLog, numbersOfBits, symbols);
				stream4bitsConsumed = DecodeSymbol(outputWriter, output4 + 1, stream4bits, stream4bitsConsumed, tableLog, numbersOfBits, symbols);

				stream1bitsConsumed = DecodeSymbol(outputWriter, output1 + 2, stream1bits, stream1bitsConsumed, tableLog, numbersOfBits, symbols);
				stream2bitsConsumed = DecodeSymbol(outputWriter, output2 + 2, stream2bits, stream2bitsConsumed, tableLog, numbersOfBits, symbols);
				stream3bitsConsumed = DecodeSymbol(outputWriter, output3 + 2, stream3bits, stream3bitsConsumed, tableLog, numbersOfBits, symbols);
				stream4bitsConsumed = DecodeSymbol(outputWriter, output4 + 2, stream4bits, stream4bitsConsumed, tableLog, numbersOfBits, symbols);

				stream1bitsConsumed = DecodeSymbol(outputWriter, output1 + 3, stream1bits, stream1bitsConsumed, tableLog, numbersOfBits, symbols);
				stream2bitsConsumed = DecodeSymbol(outputWriter, output2 + 3, stream2bits, stream2bitsConsumed, tableLog, numbersOfBits, symbols);
				stream3bitsConsumed = DecodeSymbol(outputWriter, output3 + 3, stream3bits, stream3bitsConsumed, tableLog, numbersOfBits, symbols);
				stream4bitsConsumed = DecodeSymbol(outputWriter, output4 + 3, stream4bits, stream4bitsConsumed, tableLog, numbersOfBits, symbols);

				output1 += Constants.SIZE_OF_INT;
				output2 += Constants.SIZE_OF_INT;
				output3 += Constants.SIZE_OF_INT;
				output4 += Constants.SIZE_OF_INT;

				BitInputStreamLoader loader = new BitInputStreamLoader(inputReader, start1, stream1currentAddress, stream1bits, stream1bitsConsumed);
				bool done = loader.Load();
				stream1bitsConsumed = loader.GetBitsConsumed();
				stream1bits = loader.GetBits();
				stream1currentAddress = loader.GetCurrentAddress();

				if (done) 
				{
					break;
				}

				loader = new BitInputStreamLoader(inputReader, start2, stream2currentAddress, stream2bits, stream2bitsConsumed);
				done = loader.Load();
				stream2bitsConsumed = loader.GetBitsConsumed();
				stream2bits = loader.GetBits();
				stream2currentAddress = loader.GetCurrentAddress();

				if (done) 
				{
					break;
				}

				loader = new BitInputStreamLoader(inputReader, start3, stream3currentAddress, stream3bits, stream3bitsConsumed);
				done = loader.Load();
				stream3bitsConsumed = loader.GetBitsConsumed();
				stream3bits = loader.GetBits();
				stream3currentAddress = loader.GetCurrentAddress();
				if (done) 
				{
					break;
				}

				loader = new BitInputStreamLoader(inputReader, start4, stream4currentAddress, stream4bits, stream4bitsConsumed);
				done = loader.Load();
				stream4bitsConsumed = loader.GetBitsConsumed();
				stream4bits = loader.GetBits();
				stream4currentAddress = loader.GetCurrentAddress();
				if (done) 
				{
					break;
				}
			}

			Util.Verify(output1 <= outputStart2 && output2 <= outputStart3 && output3 <= outputStart4, inputAddress, "Input is corrupted");

			/// finish streams one by one
			DecodeTail(inputReader, start1, stream1currentAddress, stream1bitsConsumed, stream1bits, outputWriter, output1, outputStart2);
			DecodeTail(inputReader, start2, stream2currentAddress, stream2bitsConsumed, stream2bits, outputWriter, output2, outputStart3);
			DecodeTail(inputReader, start3, stream3currentAddress, stream3bitsConsumed, stream3bits, outputWriter, output3, outputStart4);
			DecodeTail(inputReader, start4, stream4currentAddress, stream4bitsConsumed, stream4bits, outputWriter, output4, outputLimit);
		}

		private void DecodeTail(BinaryReader inputReader, long startAddress, long currentAddress, int bitsConsumed, long bits, BinaryWriter outputWriter, long outputAddress, long outputLimit)
		{
			int tableLog = this.tableLog;
			byte[] numbersOfBits = this.numbersOfBits;
			byte[] symbols = this.symbols;

			// closer to the end
			while (outputAddress < outputLimit) 
			{
				BitInputStreamLoader loader = new BitInputStreamLoader(inputReader, startAddress, currentAddress, bits, bitsConsumed);
				bool done = loader.Load();
				bitsConsumed = loader.GetBitsConsumed();
				bits = loader.GetBits();
				currentAddress = loader.GetCurrentAddress();
				if (done) 
				{
					break;
				}

				bitsConsumed = DecodeSymbol(outputWriter, outputAddress++, bits, bitsConsumed, tableLog, numbersOfBits, symbols);
			}

			// not more data in bit stream, so no need to reload
			while (outputAddress < outputLimit) 
			{
				bitsConsumed = DecodeSymbol(outputWriter, outputAddress++, bits, bitsConsumed, tableLog, numbersOfBits, symbols);
			}

			Util.Verify(BitInputStream.IsEndOfStream(startAddress, currentAddress, bitsConsumed), startAddress, "Bit stream is not fully consumed");
		}

		private static int DecodeSymbol(BinaryWriter outputWriter, long outputAddress, long bitContainer, int bitsConsumed, int tableLog, byte[] numbersOfBits, byte[] symbols)
		{
			int value = (int) BitInputStream.PeekBitsFast(bitsConsumed, bitContainer, tableLog);
			outputWriter.BaseStream.Seek(outputAddress, SeekOrigin.Begin);
			outputWriter.Write(symbols[value]);
			return bitsConsumed + numbersOfBits[value];
		}
	}
}