using System;
using System.IO;

namespace CSharp_zstd
{
	public class FiniteStateEntropyTable
	{
		public int log2Size;
		public readonly int[] newState;
		public readonly byte[] symbol;
		public readonly byte[] numberOfBits;

		public FiniteStateEntropyTable(int log2Capacity)
		{
			int capacity = 1 << log2Capacity;
			newState = new int[capacity];
			symbol = new byte[capacity];
			numberOfBits = new byte[capacity];
		}

		public FiniteStateEntropyTable(int log2Size, int[] newState, byte[] symbol, byte[] numberOfBits)
		{
			int size = 1 << log2Size;
			if (newState.Length != size || symbol.Length != size || numberOfBits.Length != size) 
			{
				throw new ArgumentException("Expected arrays to match provided size");
			}

			this.log2Size = log2Size;
			this.newState = newState;
			this.symbol = symbol;
			this.numberOfBits = numberOfBits;
		}
	}

	public class FiniteStateEntropy
	{
		public static readonly int MAX_SYMBOL = 255;
		public static readonly int MAX_TABLE_LOG = 12;
		public static readonly int MIN_TABLE_LOG = 5;

		private static readonly int[] REST_TO_BEAT = new int[] { 0, 473195, 504333, 520860, 550000, 700000, 750000, 830000 };
		private static readonly short UNASSIGNED = -2;

		public static int Decompress(FiniteStateEntropyTable table, BinaryReader inputReader, long inputAddress, long inputLimit, byte[] outputBuffer)
		{
			BinaryWriter outputWriter = new BinaryWriter(new MemoryStream(outputBuffer));
			long outputAddress = 0;
			long outputLimit = outputAddress + outputBuffer.Length;

			long input = inputAddress;
			long output = outputAddress;

			// initialize bit stream
			BitInputStreamInitializer initializer = new BitInputStreamInitializer(inputReader, input, inputLimit);
			initializer.Initialize();
			int bitsConsumed = initializer.GetBitsConsumed();
			long currentAddress = initializer.GetCurrentAddress();
			long bits = initializer.GetBits();

			// initialize first FSE stream
			int state1 = (int) BitInputStream.PeekBits(bitsConsumed, bits, table.log2Size);
			bitsConsumed += table.log2Size;

			BitInputStreamLoader loader = new BitInputStreamLoader(inputReader, input, currentAddress, bits, bitsConsumed);
			loader.Load();
			bits = loader.GetBits();
			bitsConsumed = loader.GetBitsConsumed();
			currentAddress = loader.GetCurrentAddress();

			// initialize second FSE stream
			int state2 = (int) BitInputStream.PeekBits(bitsConsumed, bits, table.log2Size);
			bitsConsumed += table.log2Size;

			loader = new BitInputStreamLoader(inputReader, input, currentAddress, bits, bitsConsumed);
			loader.Load();
			bits = loader.GetBits();
			bitsConsumed = loader.GetBitsConsumed();
			currentAddress = loader.GetCurrentAddress();

			byte[] symbols = table.symbol;
			byte[] numbersOfBits = table.numberOfBits;
			int[] newStates = table.newState;

			// decode 4 symbols per loop
			while (output <= outputLimit - 4) 
			{
				int numberOfBits;

				outputWriter.BaseStream.Seek(output, SeekOrigin.Begin);
				outputWriter.Write(symbols[state1]);
				numberOfBits = numbersOfBits[state1];
				state1 = (int) (newStates[state1] + BitInputStream.PeekBits(bitsConsumed, bits, numberOfBits));
				bitsConsumed += numberOfBits;

				outputWriter.BaseStream.Seek(output + 1, SeekOrigin.Begin);
				outputWriter.Write(symbols[state2]);
				numberOfBits = numbersOfBits[state2];
				state2 = (int) (newStates[state2] + BitInputStream.PeekBits(bitsConsumed, bits, numberOfBits));
				bitsConsumed += numberOfBits;

				outputWriter.BaseStream.Seek(output + 2, SeekOrigin.Begin);
				outputWriter.Write(symbols[state1]);
				numberOfBits = numbersOfBits[state1];
				state1 = (int) (newStates[state1] + BitInputStream.PeekBits(bitsConsumed, bits, numberOfBits));
				bitsConsumed += numberOfBits;

				outputWriter.BaseStream.Seek(output + 3, SeekOrigin.Begin);
				outputWriter.Write(symbols[state2]);
				numberOfBits = numbersOfBits[state2];
				state2 = (int) (newStates[state2] + BitInputStream.PeekBits(bitsConsumed, bits, numberOfBits));
				bitsConsumed += numberOfBits;

				output += Constants.SIZE_OF_INT;

				loader = new BitInputStreamLoader(inputReader, input, currentAddress, bits, bitsConsumed);
				bool done = loader.Load();
				bitsConsumed = loader.GetBitsConsumed();
				bits = loader.GetBits();
				currentAddress = loader.GetCurrentAddress();
				if (done) 
				{
					break;
				}
			}

			while (true) 
			{
				Util.Verify(output <= outputLimit - 2, input, "Output buffer is too small");
				outputWriter.BaseStream.Seek(output++, SeekOrigin.Begin);
				outputWriter.Write(symbols[state1]);
				int numberOfBits = numbersOfBits[state1];
				state1 = (int) (newStates[state1] + BitInputStream.PeekBits(bitsConsumed, bits, numberOfBits));
				bitsConsumed += numberOfBits;

				loader = new BitInputStreamLoader(inputReader, input, currentAddress, bits, bitsConsumed);
				loader.Load();
				bitsConsumed = loader.GetBitsConsumed();
				bits = loader.GetBits();
				currentAddress = loader.GetCurrentAddress();

				if (loader.IsOverflow()) 
				{
					outputWriter.BaseStream.Seek(output++, SeekOrigin.Begin);
					outputWriter.Write(symbols[state2]);
					break;
				}

				Util.Verify(output <= outputLimit - 2, input, "Output buffer is too small");
				outputWriter.BaseStream.Seek(output++, SeekOrigin.Begin);
				outputWriter.Write(symbols[state2]);
				int numberOfBits1 = numbersOfBits[state2];
				state2 = (int) (newStates[state2] + BitInputStream.PeekBits(bitsConsumed, bits, numberOfBits1));
				bitsConsumed += numberOfBits1;

				loader = new BitInputStreamLoader(inputReader, input, currentAddress, bits, bitsConsumed);
				loader.Load();
				bitsConsumed = loader.GetBitsConsumed();
				bits = loader.GetBits();
				currentAddress = loader.GetCurrentAddress();

				if (loader.IsOverflow()) 
				{
					outputWriter.BaseStream.Seek(output++, SeekOrigin.Begin);
					outputWriter.Write(symbols[state1]);
					break;
				}
			}

			return (int) (output - outputAddress);
		}
	}
}