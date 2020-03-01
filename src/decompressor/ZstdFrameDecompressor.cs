using System;
using System.IO;

namespace CSharp_zstd
{
	public class ZstdFrameDecompressor
	{
		private static readonly int[] DEC_32_TABLE = { 4, 1, 2, 1, 4, 4, 4, 4 };
		private static readonly int[] DEC_64_TABLE = { 0, 0, 0, -1, 0, 1, 2, 3 };

		private static readonly uint V07_MAGIC_NUMBER = 0xFD2FB527;

		private static readonly int MAX_WINDOW_SIZE = 1 << 23;

		private static readonly int[] LITERALS_LENGTH_BASE = {
				0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
				16, 18, 20, 22, 24, 28, 32, 40, 48, 64, 0x80, 0x100, 0x200, 0x400, 0x800, 0x1000,
				0x2000, 0x4000, 0x8000, 0x10000 
				};

		private static readonly int[] MATCH_LENGTH_BASE = {
				3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18,
				19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34,
				35, 37, 39, 41, 43, 47, 51, 59, 67, 83, 99, 0x83, 0x103, 0x203, 0x403, 0x803,
				0x1003, 0x2003, 0x4003, 0x8003, 0x10003 
				};

		private static readonly int[] OFFSET_CODES_BASE = {
				0, 1, 1, 5, 0xD, 0x1D, 0x3D, 0x7D,
				0xFD, 0x1FD, 0x3FD, 0x7FD, 0xFFD, 0x1FFD, 0x3FFD, 0x7FFD,
				0xFFFD, 0x1FFFD, 0x3FFFD, 0x7FFFD, 0xFFFFD, 0x1FFFFD, 0x3FFFFD, 0x7FFFFD,
				0xFFFFFD, 0x1FFFFFD, 0x3FFFFFD, 0x7FFFFFD, 0xFFFFFFD 
				};

		private static readonly FiniteStateEntropyTable DEFAULT_LITERALS_LENGTH_TABLE = new FiniteStateEntropyTable(
            6,
            new int[] {
                    0, 16, 32, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 32, 0, 0, 0, 0, 32, 0, 0, 32, 0, 32, 0, 32, 0, 0, 32, 0, 32, 0, 32, 0, 0, 16, 32, 0, 0, 48, 16, 32, 32, 32,
                    32, 32, 32, 32, 32, 0, 32, 32, 32, 32, 32, 32, 0, 0, 0, 0 },
            new byte[] {
                    0, 0, 1, 3, 4, 6, 7, 9, 10, 12, 14, 16, 18, 19, 21, 22, 24, 25, 26, 27, 29, 31, 0, 1, 2, 4, 5, 7, 8, 10, 11, 13, 16, 17, 19, 20, 22, 23, 25, 25, 26, 28, 30, 0,
                    1, 2, 3, 5, 6, 8, 9, 11, 12, 15, 17, 18, 20, 21, 23, 24, 35, 34, 33, 32 },
            new byte[] {
                    4, 4, 5, 5, 5, 5, 5, 5, 5, 5, 6, 5, 5, 5, 5, 5, 5, 5, 5, 6, 6, 6, 4, 4, 5, 5, 5, 5, 5, 5, 5, 6, 5, 5, 5, 5, 5, 5, 4, 4, 5, 6, 6, 4, 4, 5, 5, 5, 5, 5, 5, 5, 5,
                    6, 5, 5, 5, 5, 5, 5, 6, 6, 6, 6 } );

		private static readonly FiniteStateEntropyTable DEFAULT_OFFSET_CODES_TABLE = new FiniteStateEntropyTable(
				5,
				new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 16, 0, 0, 0, 0, 16, 0, 0, 0, 16, 0, 0, 0, 0, 0, 0, 0 },
				new byte[] { 0, 6, 9, 15, 21, 3, 7, 12, 18, 23, 5, 8, 14, 20, 2, 7, 11, 17, 22, 4, 8, 13, 19, 1, 6, 10, 16, 28, 27, 26, 25, 24 },
				new byte[] { 5, 4, 5, 5, 5, 5, 4, 5, 5, 5, 5, 4, 5, 5, 5, 4, 5, 5, 5, 5, 4, 5, 5, 5, 4, 5, 5, 5, 5, 5, 5, 5 } );

		private static readonly FiniteStateEntropyTable DEFAULT_MATCH_LENGTH_TABLE = new FiniteStateEntropyTable(
				6,
				new int[] {
						0, 0, 32, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 16, 0, 32, 0, 32, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 32, 48, 16, 32, 32, 32, 32,
						0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
				new byte[] {
						0, 1, 2, 3, 5, 6, 8, 10, 13, 16, 19, 22, 25, 28, 31, 33, 35, 37, 39, 41, 43, 45, 1, 2, 3, 4, 6, 7, 9, 12, 15, 18, 21, 24, 27, 30, 32, 34, 36, 38, 40, 42, 44, 1,
						1, 2, 4, 5, 7, 8, 11, 14, 17, 20, 23, 26, 29, 52, 51, 50, 49, 48, 47, 46 },
				new byte[] {
						6, 4, 5, 5, 5, 5, 5, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 4, 4, 5, 5, 5, 5, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 4, 4, 4, 5, 5, 5, 5, 6, 6, 6,
						6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6 } );

		private readonly byte[] literals = new byte[Constants.MAX_BLOCK_SIZE + Constants.SIZE_OF_LONG]; // extra space to allow for long-at-a-time copy

		// current buffer containing literals
		private BinaryReader literalsBase;
		private long literalsAddress;
		private long literalsLimit;

		private readonly int[] previousOffsets = new int[3];

		private readonly FiniteStateEntropyTable literalsLengthTable = new FiniteStateEntropyTable(Constants.LITERAL_LENGTH_TABLE_LOG);
		private readonly FiniteStateEntropyTable offsetCodesTable = new FiniteStateEntropyTable(Constants.OFFSET_TABLE_LOG);
		private readonly FiniteStateEntropyTable matchLengthTable = new FiniteStateEntropyTable(Constants.MATCH_LENGTH_TABLE_LOG);

		private FiniteStateEntropyTable currentLiteralsLengthTable;
		private FiniteStateEntropyTable currentOffsetCodesTable;
		private FiniteStateEntropyTable currentMatchLengthTable;

		private readonly Huffman huffman = new Huffman();
		private readonly FseTableReader fse = new FseTableReader();

		public int Decompress(BinaryReader inputReader, long inputAddress, long inputLimit, BinaryWriter outputWriter, long outputAddress, long outputLimit)
    	{
			if (outputAddress == outputLimit) {
				return 0;
			}

			long input = inputAddress;
			long output = outputAddress;

			while (input < inputLimit) 
			{
				this.Reset();
				long outputStart = output;
				input += VerifyMagic(inputReader, inputAddress, inputLimit);

				FrameHeader frameHeader = ReadFrameHeader(inputReader, input, inputLimit);
				input += frameHeader.headerSize;

				bool lastBlock;
				do 
				{
					Util.Verify(input + Constants.SIZE_OF_BLOCK_HEADER <= inputLimit, input, "Not enough input bytes");

					// read block header
					inputReader.BaseStream.Seek(input, SeekOrigin.Begin);
					int header = inputReader.ReadInt32() & 0xFF_FFFF;
					input += Constants.SIZE_OF_BLOCK_HEADER;

					lastBlock = (header & 1) != 0;
					int blockType = (Util.UnsignedRightShift(header, 1)) & 0b11;
					int blockSize = (Util.UnsignedRightShift(header, 3)) & 0x1F_FFFF; // 21 bits

					int decodedSize = 0;
					switch (blockType) {
						case Constants.RAW_BLOCK:
							Util.Verify(inputAddress + blockSize <= inputLimit, input, "Not enough input bytes");
							decodedSize = DecodeRawBlock(inputReader, input, blockSize, outputWriter, output, outputLimit);
							input += blockSize;
							break;
						case Constants.RLE_BLOCK:
							Util.Verify(inputAddress + 1 <= inputLimit, input, "Not enough input bytes");
							decodedSize = DecodeRleBlock(blockSize, inputReader, input, outputWriter, output, outputLimit);
							input += 1;
							break;
						case Constants.COMPRESSED_BLOCK:
							Util.Verify(inputAddress + blockSize <= inputLimit, input, "Not enough input bytes");
							decodedSize = DecodeCompressedBlock(inputReader, input, blockSize, outputWriter, output, outputLimit, frameHeader.windowSize, outputAddress);
							input += blockSize;
							break;
						default:
							Util.Fail(input, "Invalid block type"); // Throws
							break;
					}

					output += decodedSize;
				}
				while (!lastBlock);

				if (frameHeader.hasChecksum) 
				{
					int decodedFrameSize = (int) (output - outputStart);

					ulong hash = XxHash64.Hash(0, new BinaryReader(outputWriter.BaseStream), outputStart, decodedFrameSize);

					inputReader.BaseStream.Seek(input, SeekOrigin.Begin);
					uint checksum = inputReader.ReadUInt32();
					if (checksum != (uint) (hash & 0x0000FFFF)) 
					{
						//throw new Exception($"Bad checksum. Expected: {checksum.ToString("X")}, actual: {((int) hash).ToString("X")}");
					}

					input += Constants.SIZE_OF_INT;
				}
			}

			return (int) (output - outputAddress);
		}

		private void Reset()
		{
			this.previousOffsets[0] = 1;
			this.previousOffsets[1] = 4;
			this.previousOffsets[2] = 8;

			this.currentLiteralsLengthTable = null;
			this.currentOffsetCodesTable = null;
			this.currentMatchLengthTable = null;
		}

		private static int DecodeRawBlock(BinaryReader inputReader, long inputAddress, int blockSize, BinaryWriter outputWriter, long outputAddress, long outputLimit)
		{
			Util.Verify(outputAddress + blockSize <= outputLimit, inputAddress, "Output buffer too small");
			inputReader.BaseStream.Seek(inputAddress, SeekOrigin.Begin);
			outputWriter.BaseStream.Seek(outputAddress, SeekOrigin.Begin);
			for (int i = 0; i < blockSize; i++)
			{
				outputWriter.Write(inputReader.ReadByte());
			}

			return blockSize;
		}

		private static int DecodeRleBlock(int size, BinaryReader inputReader, long inputAddress, BinaryWriter outputWriter, long outputAddress, long outputLimit)
		{
			Util.Verify(outputAddress + size <= outputLimit, inputAddress, "Output buffer too small");

			long output = outputAddress;
			inputReader.BaseStream.Seek(inputAddress, SeekOrigin.Begin);
			long value = inputReader.ReadByte() & 0xFFL;

			int remaining = size;
			if (remaining >= Constants.SIZE_OF_LONG) 
			{
				long packed = value
						| (value << 8)
						| (value << 16)
						| (value << 24)
						| (value << 32)
						| (value << 40)
						| (value << 48)
						| (value << 56);

				do 
				{
					outputWriter.BaseStream.Seek(output, SeekOrigin.Begin);
					outputWriter.Write(packed);
					output += Constants.SIZE_OF_LONG;
					remaining -= Constants.SIZE_OF_LONG;
				}
				while (remaining >= Constants.SIZE_OF_LONG);
			}

			for (int i = 0; i < remaining; i++) 
			{
				outputWriter.BaseStream.Seek(output, SeekOrigin.Begin);
				outputWriter.Write((byte) value);
				output++;
			}

			return size;
		}

		private int DecodeCompressedBlock(BinaryReader inputReader, long inputAddress, int blockSize, BinaryWriter outputWriter, long outputAddress, long outputLimit, int windowSize, long outputAbsoluteBaseAddress)
		{
			long inputLimit = inputAddress + blockSize;
			long input = inputAddress;

			Util.Verify(blockSize <= Constants.MAX_BLOCK_SIZE, input, "Expected match length table to be present");
			Util.Verify(blockSize >= Constants.MIN_BLOCK_SIZE, input, "Compressed block size too small");

			// decode literals
			inputReader.BaseStream.Seek(input, SeekOrigin.Begin);
			int literalsBlockType = inputReader.ReadByte() & 0b11;

			switch (literalsBlockType) 
			{
				case Constants.RAW_LITERALS_BLOCK: 
				{
					input += DecodeRawLiterals(inputReader, input, inputLimit);
					break;
				}
				case Constants.RLE_LITERALS_BLOCK: 
				{
					input += DecodeRleLiterals(inputReader, input, blockSize);
					break;
				}
				case Constants.TREELESS_LITERALS_BLOCK:
					Util.Verify(huffman.IsLoaded(), input, "Dictionary is corrupted");
					break;
				case Constants.COMPRESSED_LITERALS_BLOCK: 
				{
					input += DecodeCompressedLiterals(inputReader, input, blockSize, literalsBlockType);
					break;
				}
				default:
					Util.Fail(input, "Invalid literals block encoding type");
					break;
			}

			Util.Verify(windowSize <= MAX_WINDOW_SIZE, input, "Window size too large (not yet supported)");

			return DecompressSequences(
					inputReader, input, inputAddress + blockSize,
					outputWriter, outputAddress, outputLimit,
					literalsBase, literalsAddress, literalsLimit,
					outputAbsoluteBaseAddress);
		}

		private int DecompressSequences(BinaryReader inputReader, long inputAddress, long inputLimit,
										BinaryWriter outputWriter, long outputAddress, long outputLimit,
				BinaryReader literalsBase, long literalsAddress, long literalsLimit,
				long outputAbsoluteBaseAddress)
		{
			long fastOutputLimit = outputLimit - Constants.SIZE_OF_LONG;
			long fastMatchOutputLimit = fastOutputLimit - Constants.SIZE_OF_LONG;

			long input = inputAddress;
			long output = outputAddress;

			long literalsInput = literalsAddress;

			int size = (int) (inputLimit - inputAddress);
			Util.Verify(size >= Constants.MIN_SEQUENCES_SIZE, input, "Not enough input bytes");

			// decode header
			inputReader.BaseStream.Seek(input++, SeekOrigin.Begin);
			int sequenceCount = inputReader.ReadByte() & 0xFF;
			if (sequenceCount != 0) 
			{
				if (sequenceCount == 255) 
				{
					Util.Verify(input + Constants.SIZE_OF_SHORT <= inputLimit, input, "Not enough input bytes");
					inputReader.BaseStream.Seek(input, SeekOrigin.Begin);
					sequenceCount = (inputReader.ReadInt16() & 0xFFFF) + Constants.LONG_NUMBER_OF_SEQUENCES;
					input += Constants.SIZE_OF_SHORT;
				}
				else if (sequenceCount > 127) 
				{
					Util.Verify(input < inputLimit, input, "Not enough input bytes");
					inputReader.BaseStream.Seek(input++, SeekOrigin.Begin);
					sequenceCount = ((sequenceCount - 128) << 8) + (inputReader.ReadByte() & 0xFF);
				}

				Util.Verify(input + Constants.SIZE_OF_INT <= inputLimit, input, "Not enough input bytes");

				inputReader.BaseStream.Seek(input++, SeekOrigin.Begin);
				byte type = inputReader.ReadByte();

				int literalsLengthType = (type & 0xFF) >> 6;
				int offsetCodesType = (type >> 4) & 0b11;
				int matchLengthType = (type >> 2) & 0b11;

				input = ComputeLiteralsTable(literalsLengthType, inputReader, input, inputLimit);
				input = ComputeOffsetsTable(offsetCodesType, inputReader, input, inputLimit);
				input = ComputeMatchLengthTable(matchLengthType, inputReader, input, inputLimit);

				// decompress sequences
				BitInputStreamInitializer initializer = new BitInputStreamInitializer(inputReader, input, inputLimit);
				initializer.Initialize();
				int bitsConsumed = initializer.GetBitsConsumed();
				long bits = initializer.GetBits();
				long currentAddress = initializer.GetCurrentAddress();

				FiniteStateEntropyTable currentLiteralsLengthTable = this.currentLiteralsLengthTable;
				FiniteStateEntropyTable currentOffsetCodesTable = this.currentOffsetCodesTable;
				FiniteStateEntropyTable currentMatchLengthTable = this.currentMatchLengthTable;

				int literalsLengthState = (int) BitInputStream.PeekBits(bitsConsumed, bits, currentLiteralsLengthTable.log2Size);
				bitsConsumed += currentLiteralsLengthTable.log2Size;

				int offsetCodesState = (int) BitInputStream.PeekBits(bitsConsumed, bits, currentOffsetCodesTable.log2Size);
				bitsConsumed += currentOffsetCodesTable.log2Size;

				int matchLengthState = (int) BitInputStream.PeekBits(bitsConsumed, bits, currentMatchLengthTable.log2Size);
				bitsConsumed += currentMatchLengthTable.log2Size;

				int[] previousOffsets = this.previousOffsets;

				byte[] literalsLengthNumbersOfBits = currentLiteralsLengthTable.numberOfBits;
				int[] literalsLengthNewStates = currentLiteralsLengthTable.newState;
				byte[] literalsLengthSymbols = currentLiteralsLengthTable.symbol;

				byte[] matchLengthNumbersOfBits = currentMatchLengthTable.numberOfBits;
				int[] matchLengthNewStates = currentMatchLengthTable.newState;
				byte[] matchLengthSymbols = currentMatchLengthTable.symbol;

				byte[] offsetCodesNumbersOfBits = currentOffsetCodesTable.numberOfBits;
				int[] offsetCodesNewStates = currentOffsetCodesTable.newState;
				byte[] offsetCodesSymbols = currentOffsetCodesTable.symbol;

				while (sequenceCount > 0) 
				{
					sequenceCount--;

					BitInputStreamLoader loader = new BitInputStreamLoader(inputReader, input, currentAddress, bits, bitsConsumed);
					loader.Load();
					bitsConsumed = loader.GetBitsConsumed();
					bits = loader.GetBits();
					currentAddress = loader.GetCurrentAddress();
					if (loader.IsOverflow()) 
					{
						Util.Verify(sequenceCount == 0, input, "Not all sequences were consumed");
						break;
					}

					// decode sequence
					int literalsLengthCode = literalsLengthSymbols[literalsLengthState];
					int matchLengthCode = matchLengthSymbols[matchLengthState];
					int offsetCode = offsetCodesSymbols[offsetCodesState];

					int literalsLengthBits = Constants.LITERALS_LENGTH_BITS[literalsLengthCode];
					int matchLengthBits = Constants.MATCH_LENGTH_BITS[matchLengthCode];
					int offsetBits = offsetCode;

					int offset = OFFSET_CODES_BASE[offsetCode];
					if (offsetCode > 0) 
					{
						offset += (int)BitInputStream.PeekBits(bitsConsumed, bits, offsetBits);
						bitsConsumed += offsetBits;
					}

					if (offsetCode <= 1) 
					{
						if (literalsLengthCode == 0) 
						{
							offset++;
						}

						if (offset != 0) 
						{
							int temp;
							if (offset == 3) 
							{
								temp = previousOffsets[0] - 1;
							}
							else 
							{
								temp = previousOffsets[offset];
							}

							if (temp == 0) 
							{
								temp = 1;
							}

							if (offset != 1) 
							{
								previousOffsets[2] = previousOffsets[1];
							}
							previousOffsets[1] = previousOffsets[0];
							previousOffsets[0] = temp;

							offset = temp;
						}
						else 
						{
							offset = previousOffsets[0];
						}
					}
					else 
					{
						previousOffsets[2] = previousOffsets[1];
						previousOffsets[1] = previousOffsets[0];
						previousOffsets[0] = offset;
					}

					int matchLength = MATCH_LENGTH_BASE[matchLengthCode];
					if (matchLengthCode > 31) 
					{
						matchLength += (int)BitInputStream.PeekBits(bitsConsumed, bits, matchLengthBits);
						bitsConsumed += matchLengthBits;
					}

					int literalsLength = LITERALS_LENGTH_BASE[literalsLengthCode];
					if (literalsLengthCode > 15) 
					{
						literalsLength += (int)BitInputStream.PeekBits(bitsConsumed, bits, literalsLengthBits);
						bitsConsumed += literalsLengthBits;
					}

					int totalBits = literalsLengthBits + matchLengthBits + offsetBits;
					if (totalBits > 64 - 7 - (Constants.LITERAL_LENGTH_TABLE_LOG + Constants.MATCH_LENGTH_TABLE_LOG + Constants.OFFSET_TABLE_LOG)) 
					{
						BitInputStreamLoader loader1 = new BitInputStreamLoader(inputReader, input, currentAddress, bits, bitsConsumed);
						loader1.Load();

						bitsConsumed = loader1.GetBitsConsumed();
						bits = loader1.GetBits();
						currentAddress = loader1.GetCurrentAddress();
					}

					int numberOfBits;

					numberOfBits = literalsLengthNumbersOfBits[literalsLengthState];
					literalsLengthState = (int) (literalsLengthNewStates[literalsLengthState] + BitInputStream.PeekBits(bitsConsumed, bits, numberOfBits)); // <= 9 bits
					bitsConsumed += numberOfBits;

					numberOfBits = matchLengthNumbersOfBits[matchLengthState];
					matchLengthState = (int) (matchLengthNewStates[matchLengthState] + BitInputStream.PeekBits(bitsConsumed, bits, numberOfBits)); // <= 9 bits
					bitsConsumed += numberOfBits;

					numberOfBits = offsetCodesNumbersOfBits[offsetCodesState];
					offsetCodesState = (int) (offsetCodesNewStates[offsetCodesState] + BitInputStream.PeekBits(bitsConsumed, bits, numberOfBits)); // <= 8 bits
					bitsConsumed += numberOfBits;

					long literalOutputLimit = output + literalsLength;
					long matchOutputLimit = literalOutputLimit + matchLength;

					Util.Verify(matchOutputLimit <= outputLimit, input, "Output buffer too small");
					long literalEnd = literalsInput + literalsLength;
					Util.Verify(literalEnd <= literalsLimit, input, "Input is corrupted");

					long matchAddress = literalOutputLimit - offset;
					Util.Verify(matchAddress >= outputAbsoluteBaseAddress, input, "Input is corrupted");

					if (literalOutputLimit > fastOutputLimit) 
					{
						ExecuteLastSequence(outputWriter, output, literalOutputLimit, matchOutputLimit, fastOutputLimit, literalsInput, matchAddress);
					}
					else 
					{
						// copy literals. literalOutputLimit <= fastOutputLimit, so we can copy
						// long at a time with over-copy
						output = CopyLiterals(outputWriter, literalsBase, output, literalsInput, literalOutputLimit);
						CopyMatch(outputWriter, fastOutputLimit, output, offset, matchOutputLimit, matchAddress, matchLength, fastMatchOutputLimit);
					}
					output = matchOutputLimit;
					literalsInput = literalEnd;
				}
			}

			// last literal segment
			output = CopyLastLiteral(outputWriter, literalsBase, literalsLimit, output, literalsInput);

			return (int) (output - outputAddress);
		}

		private long CopyLastLiteral(BinaryWriter outputWriter, BinaryReader literalsBase, long literalsLimit, long output, long literalsInput)
		{
			long lastLiteralsSize = literalsLimit - literalsInput;
			outputWriter.BaseStream.Seek(output, SeekOrigin.Begin);
			literalsBase.BaseStream.Seek(literalsInput, SeekOrigin.Begin);
			outputWriter.Write(literalsBase.ReadBytes((int)lastLiteralsSize));
			output += lastLiteralsSize;
			return output;
		}

		private void CopyMatch(BinaryWriter outputWriter, long fastOutputLimit, long output, int offset, long matchOutputLimit, long matchAddress, int matchLength, long fastMatchOutputLimit)
		{
			matchAddress = CopyMatchHead(outputWriter, output, offset, matchAddress);
			output += Constants.SIZE_OF_LONG;
			matchLength -= Constants.SIZE_OF_LONG; // first 8 bytes copied above

			CopyMatchTail(outputWriter, fastOutputLimit, output, matchOutputLimit, matchAddress, matchLength, fastMatchOutputLimit);
		}

		private void CopyMatchTail(BinaryWriter outputWriter, long fastOutputLimit, long output, long matchOutputLimit, long matchAddress, int matchLength, long fastMatchOutputLimit)
		{
			// fastMatchOutputLimit is just fastOutputLimit - SIZE_OF_LONG. It needs to be passed in so that it can be computed once for the
			// whole invocation to decompressSequences. Otherwise, we'd just compute it here.
			// If matchOutputLimit is < fastMatchOutputLimit, we know that even after the head (8 bytes) has been copied, the output pointer
			// will be within fastOutputLimit, so it's safe to copy blindly before checking the limit condition
			if (matchOutputLimit < fastMatchOutputLimit) 
			{
				int copied = 0;
				BinaryReader tempReader = new BinaryReader(outputWriter.BaseStream);
				do 
				{
					tempReader.BaseStream.Seek(matchAddress, SeekOrigin.Begin);
					long valueToCopy = tempReader.ReadInt64();
					outputWriter.BaseStream.Seek(output, SeekOrigin.Begin);
					outputWriter.Write(valueToCopy);
					output += Constants.SIZE_OF_LONG;
					matchAddress += Constants.SIZE_OF_LONG;
					copied += Constants.SIZE_OF_LONG;
				}
				while (copied < matchLength);
			}
			else 
			{
				BinaryReader tempReader = new BinaryReader(outputWriter.BaseStream);
				while (output < fastOutputLimit) 
				{
					tempReader.BaseStream.Seek(matchAddress, SeekOrigin.Begin);
					long valueToCopy = tempReader.ReadInt64();
					outputWriter.BaseStream.Seek(output, SeekOrigin.Begin);
					outputWriter.Write(valueToCopy);
					matchAddress += Constants.SIZE_OF_LONG;
					output += Constants.SIZE_OF_LONG;
				}

				while (output < matchOutputLimit) 
				{
					tempReader.BaseStream.Seek(matchAddress++, SeekOrigin.Begin);
					byte valueToCopy = tempReader.ReadByte();
					outputWriter.BaseStream.Seek(output++, SeekOrigin.Begin);
					outputWriter.Write(valueToCopy);
				}
			}
		}

		private long CopyMatchHead(BinaryWriter outputWriter, long output, int offset, long matchAddress)
		{
			BinaryReader tempReader = new BinaryReader(outputWriter.BaseStream);
			// copy match
			if (offset < 8) 
			{
				// 8 bytes apart so that we can copy long-at-a-time below
				int increment32 = DEC_32_TABLE[offset];
				int decrement64 = DEC_64_TABLE[offset];

				tempReader.BaseStream.Seek(matchAddress, SeekOrigin.Begin);
				byte valueToCopy = tempReader.ReadByte();
				outputWriter.BaseStream.Seek(output, SeekOrigin.Begin);
				outputWriter.Write(valueToCopy);
				tempReader.BaseStream.Seek(matchAddress + 1, SeekOrigin.Begin);
				valueToCopy = tempReader.ReadByte();
				outputWriter.BaseStream.Seek(output + 1, SeekOrigin.Begin);
				outputWriter.Write(valueToCopy);
				tempReader.BaseStream.Seek(matchAddress + 2, SeekOrigin.Begin);
				valueToCopy = tempReader.ReadByte();
				outputWriter.BaseStream.Seek(output + 2, SeekOrigin.Begin);
				outputWriter.Write(valueToCopy);
				tempReader.BaseStream.Seek(matchAddress + 3, SeekOrigin.Begin);
				valueToCopy = tempReader.ReadByte();
				outputWriter.BaseStream.Seek(output + 3, SeekOrigin.Begin);
				outputWriter.Write(valueToCopy);
				matchAddress += increment32;

				tempReader.BaseStream.Seek(matchAddress, SeekOrigin.Begin);
				int int32ToCopy = tempReader.ReadInt32();
				outputWriter.BaseStream.Seek(output + 4, SeekOrigin.Begin);
				outputWriter.Write(int32ToCopy);
				matchAddress -= decrement64;
			}
			else 
			{
				tempReader.BaseStream.Seek(matchAddress, SeekOrigin.Begin);
				long int64ToCopy = tempReader.ReadInt64();
				outputWriter.BaseStream.Seek(output, SeekOrigin.Begin);
				outputWriter.Write(int64ToCopy);
				matchAddress += Constants.SIZE_OF_LONG;
			}
			return matchAddress;
		}

		private long CopyLiterals(BinaryWriter outputWriter, BinaryReader literalsBase, long output, long literalsInput, long literalOutputLimit)
		{
			long literalInput = literalsInput;
			do 
			{
				outputWriter.BaseStream.Seek(output, SeekOrigin.Begin);
				literalsBase.BaseStream.Seek(literalInput, SeekOrigin.Begin);
				outputWriter.Write(literalsBase.ReadInt64());
				output += Constants.SIZE_OF_LONG;
				literalInput += Constants.SIZE_OF_LONG;
			}
			while (output < literalOutputLimit);
			output = literalOutputLimit; // correction in case we over-copied
			return output;
		}

		private long ComputeMatchLengthTable(int matchLengthType, BinaryReader inputReader, long input, long inputLimit)
		{
			switch (matchLengthType) 
			{
				case Constants.SEQUENCE_ENCODING_RLE:
					Util.Verify(input < inputLimit, input, "Not enough input bytes");

					inputReader.BaseStream.Seek(input++, SeekOrigin.Begin);
					byte value = inputReader.ReadByte();
					Util.Verify(value <= Constants.MAX_MATCH_LENGTH_SYMBOL, input, "Value exceeds expected maximum value");

					FseTableReader.InitializeRleTable(matchLengthTable, value);
					currentMatchLengthTable = matchLengthTable;
					break;
				case Constants.SEQUENCE_ENCODING_BASIC:
					currentMatchLengthTable = DEFAULT_MATCH_LENGTH_TABLE;
					break;
				case Constants.SEQUENCE_ENCODING_REPEAT:
					Util.Verify(currentMatchLengthTable != null, input, "Expected match length table to be present");
					break;
				case Constants.SEQUENCE_ENCODING_COMPRESSED:
					input += fse.ReadFseTable(matchLengthTable, inputReader, input, inputLimit, Constants.MAX_MATCH_LENGTH_SYMBOL, Constants.MATCH_LENGTH_TABLE_LOG);
					currentMatchLengthTable = matchLengthTable;
					break;
				default:
					Util.Fail(input, "Invalid match length encoding type"); // Throws
					break;
			}
			return input;
		}

		private long ComputeOffsetsTable(int offsetCodesType, BinaryReader inputReader, long input, long inputLimit)
		{
			switch (offsetCodesType) 
			{
				case Constants.SEQUENCE_ENCODING_RLE:
					Util.Verify(input < inputLimit, input, "Not enough input bytes");

					inputReader.BaseStream.Seek(input++, SeekOrigin.Begin);
					byte value = inputReader.ReadByte();
					Util.Verify(value <= Constants.DEFAULT_MAX_OFFSET_CODE_SYMBOL, input, "Value exceeds expected maximum value");

					FseTableReader.InitializeRleTable(offsetCodesTable, value);
					currentOffsetCodesTable = offsetCodesTable;
					break;
				case Constants.SEQUENCE_ENCODING_BASIC:
					currentOffsetCodesTable = DEFAULT_OFFSET_CODES_TABLE;
					break;
				case Constants.SEQUENCE_ENCODING_REPEAT:
					Util.Verify(currentOffsetCodesTable != null, input, "Expected match length table to be present");
					break;
				case Constants.SEQUENCE_ENCODING_COMPRESSED:
					input += fse.ReadFseTable(offsetCodesTable, inputReader, input, inputLimit, Constants.DEFAULT_MAX_OFFSET_CODE_SYMBOL, Constants.OFFSET_TABLE_LOG);
					currentOffsetCodesTable = offsetCodesTable;
					break;
				default:
					Util.Fail(input, "Invalid offset code encoding type"); // Throws
					break;
			}
			return input;
		}

		private long ComputeLiteralsTable(int literalsLengthType, BinaryReader inputReader, long input, long inputLimit)
		{
			switch (literalsLengthType) 
			{
				case Constants.SEQUENCE_ENCODING_RLE:
					Util.Verify(input < inputLimit, input, "Not enough input bytes");

					inputReader.BaseStream.Seek(input++, SeekOrigin.Begin);
					byte value = inputReader.ReadByte();
					Util.Verify(value <= Constants.MAX_LITERALS_LENGTH_SYMBOL, input, "Value exceeds expected maximum value");

					FseTableReader.InitializeRleTable(literalsLengthTable, value);
					currentLiteralsLengthTable = literalsLengthTable;
					break;
				case Constants.SEQUENCE_ENCODING_BASIC:
					currentLiteralsLengthTable = DEFAULT_LITERALS_LENGTH_TABLE;
					break;
				case Constants.SEQUENCE_ENCODING_REPEAT:
					Util.Verify(currentLiteralsLengthTable != null, input, "Expected match length table to be present");
					break;
				case Constants.SEQUENCE_ENCODING_COMPRESSED:
					input += fse.ReadFseTable(literalsLengthTable, inputReader, input, inputLimit, Constants.MAX_LITERALS_LENGTH_SYMBOL, Constants.LITERAL_LENGTH_TABLE_LOG);
					currentLiteralsLengthTable = literalsLengthTable;
					break;
				default:
					Util.Fail(input, "Invalid literals length encoding type"); // Throws
					break;
			}
			return input;
		}

		private void ExecuteLastSequence(BinaryWriter outputWriter, long output, long literalOutputLimit, long matchOutputLimit, long fastOutputLimit, long literalInput, long matchAddress)
		{
			// copy literals
			if (output < fastOutputLimit) 
			{
				// wild copy
				do 
				{
					outputWriter.BaseStream.Seek(output, SeekOrigin.Begin);
					this.literalsBase.BaseStream.Seek(literalInput, SeekOrigin.Begin);
					outputWriter.Write(this.literalsBase.ReadInt64());
					output += Constants.SIZE_OF_LONG;
					literalInput += Constants.SIZE_OF_LONG;
				}
				while (output < fastOutputLimit);

				literalInput -= output - fastOutputLimit;
				output = fastOutputLimit;
			}

			while (output < literalOutputLimit) 
			{
				outputWriter.BaseStream.Seek(output, SeekOrigin.Begin);
				this.literalsBase.BaseStream.Seek(literalInput, SeekOrigin.Begin);
				outputWriter.Write(this.literalsBase.ReadByte());
				output++;
				literalInput++;
			}

			// copy match
			BinaryReader tempReader = new BinaryReader(outputWriter.BaseStream);
			while (output < matchOutputLimit) 
			{
				tempReader.BaseStream.Seek(matchAddress, SeekOrigin.Begin);
				byte valueToCopy = tempReader.ReadByte();
				outputWriter.BaseStream.Seek(output, SeekOrigin.Begin);
				outputWriter.Write(valueToCopy);
				output++;
				matchAddress++;
			}
		}

		private int DecodeCompressedLiterals(BinaryReader inputReader, long inputAddress, int blockSize, int literalsBlockType)
		{
			long input = inputAddress;
			Util.Verify(blockSize >= 5, input, "Not enough input bytes");

			// compressed
			int compressedSize = 0;
			int uncompressedSize = 0;
			bool singleStream = false;
			int headerSize = 0;
			inputReader.BaseStream.Seek(input, SeekOrigin.Begin);
			int type = (inputReader.ReadByte() >> 2) & 0b11;
			switch (type) 
			{
				case 0:
					singleStream = true;
					goto case 1;
				case 1: 
				{
					inputReader.BaseStream.Seek(input, SeekOrigin.Begin);
					int header = inputReader.ReadInt32();

					headerSize = 3;
					uncompressedSize = Util.UnsignedRightShift(header, 4) & Util.Mask(10);
					compressedSize = Util.UnsignedRightShift(header, 14) & Util.Mask(10);
					break;
				}
				case 2: 
				{
					inputReader.BaseStream.Seek(input, SeekOrigin.Begin);
					int header = inputReader.ReadInt32();

					headerSize = 4;
					uncompressedSize = Util.UnsignedRightShift(header, 4) & Util.Mask(14);
					compressedSize = Util.UnsignedRightShift(header, 18) & Util.Mask(14);
					break;
				}
				case 3: 
				{
					// read 5 little-endian bytes
					inputReader.BaseStream.Seek(input, SeekOrigin.Begin);
					byte b = inputReader.ReadByte();
					inputReader.BaseStream.Seek(input + 1, SeekOrigin.Begin);
					long header = b & 0xFF |
							(inputReader.ReadInt32() & 0xFFFFFFFF) << 8;

					headerSize = 5;
					uncompressedSize = (int) (Util.UnsignedRightShift(header, 4) & Util.Mask(18));
					compressedSize = (int) (Util.UnsignedRightShift(header, 22) & Util.Mask(18));
					break;
				}
				default:
					Util.Fail(input, "Invalid literals header size type"); // Throws
					break;
			}

			Util.Verify(uncompressedSize <= Constants.MAX_BLOCK_SIZE, input, "Block exceeds maximum size");
			Util.Verify(headerSize + compressedSize <= blockSize, input, "Input is corrupted");

			input += headerSize;

			long inputLimit = input + compressedSize;
			if (literalsBlockType != Constants.TREELESS_LITERALS_BLOCK) 
			{
				input += huffman.ReadTable(inputReader, input, compressedSize);
			}

			literalsBase = new BinaryReader(new MemoryStream(literals));
			literalsAddress = 0;
			literalsLimit = 0 + uncompressedSize;

			if (singleStream) 
			{
				huffman.DecodeSingleStream(inputReader, input, inputLimit, new BinaryWriter(new MemoryStream(literals)), literalsAddress, literalsLimit);
			}
			else 
			{
				huffman.Decode4Streams(inputReader, input, inputLimit, new BinaryWriter(new MemoryStream(literals)), literalsAddress, literalsLimit);
			}

			return headerSize + compressedSize;
		}

		private int DecodeRleLiterals(BinaryReader inputReader, long inputAddress, int blockSize)
		{
			long input = inputAddress;
			int outputSize = 0;

			inputReader.BaseStream.Seek(input, SeekOrigin.Begin);
			int type = (inputReader.ReadByte() >> 2) & 0b11;
			switch (type) 
			{
				case 0:
				case 2:
					inputReader.BaseStream.Seek(input, SeekOrigin.Begin);
					outputSize = (inputReader.ReadByte() & 0xFF) >> 3;
					input++;
					break;
				case 1:
					inputReader.BaseStream.Seek(input, SeekOrigin.Begin);
					outputSize = Util.UnsignedRightShift((inputReader.ReadInt16() & 0xFFFF), 4);
					input += 2;
					break;
				case 3:
					// we need at least 4 bytes (3 for the header, 1 for the payload)
					Util.Verify(blockSize >= Constants.SIZE_OF_INT, input, "Not enough input bytes");
					inputReader.BaseStream.Seek(input, SeekOrigin.Begin);
					outputSize = Util.UnsignedRightShift((inputReader.ReadInt32() & 0xFF_FFFF), 4);
					input += 3;
					break;
				default:
					Util.Fail(input, "Invalid RLE literals header encoding type"); // Throws
					break;
			}

			Util.Verify(outputSize <= Constants.MAX_BLOCK_SIZE, input, "Output exceeds maximum block size");

			inputReader.BaseStream.Seek(input++, SeekOrigin.Begin);
			byte value = inputReader.ReadByte();
			//Array.Fill(literals, value, 0, outputSize + Constants.SIZE_OF_LONG);
			for (int i = 0; i < outputSize + Constants.SIZE_OF_LONG; i++)
			{
				literals[i] = value;
			}

			literalsBase = new BinaryReader(new MemoryStream(literals));
			literalsAddress = 0;
			literalsLimit = 0 + outputSize;

			return (int) (input - inputAddress);
		}

		private int DecodeRawLiterals(BinaryReader inputReader, long inputAddress, long inputLimit)
		{
			long input = inputAddress;
			inputReader.BaseStream.Seek(input, SeekOrigin.Begin);
			int type = (inputReader.ReadByte() >> 2) & 0b11;

			int literalSize = 0;
			switch (type) 
			{
				case 0:
				case 2:
					inputReader.BaseStream.Seek(input, SeekOrigin.Begin);
					literalSize = (inputReader.ReadByte() & 0xFF) >> 3;
					input++;
					break;
				case 1:
					inputReader.BaseStream.Seek(input, SeekOrigin.Begin);
					literalSize = Util.UnsignedRightShift((inputReader.ReadByte() & 0xFFFF), 4);
					input += 2;
					break;
				case 3:
					// read 3 little-endian bytes
					inputReader.BaseStream.Seek(input, SeekOrigin.Begin);
					byte b = inputReader.ReadByte();
					inputReader.BaseStream.Seek(input + 1, SeekOrigin.Begin);
					int header = ((b & 0xFF) |
							((inputReader.ReadInt16() & 0xFFFF) << 8));

					literalSize = Util.UnsignedRightShift(header, 4);
					input += 3;
					break;
				default:
					Util.Fail(input, "Invalid raw literals header encoding type"); // Throws
					break;
			}

			Util.Verify(input + literalSize <= inputLimit, input, "Not enough input bytes");

			// Set literals pointer to [input, literalSize], but only if we can copy 8 bytes at a time during sequence decoding
			// Otherwise, copy literals into buffer that's big enough to guarantee that
			if (literalSize > (inputLimit - input) - Constants.SIZE_OF_LONG) 
			{
				literalsBase = new BinaryReader( new MemoryStream(literals));
				literalsAddress = 0;
				literalsLimit = 0 + literalSize;

				inputReader.BaseStream.Seek(input, SeekOrigin.Begin);
				byte[] bytesToCopy = inputReader.ReadBytes((int)literalSize);
				BinaryWriter tempWriter = new BinaryWriter(new MemoryStream(literals));
				tempWriter.BaseStream.Seek(literalsAddress, SeekOrigin.Begin);
				tempWriter.Write(bytesToCopy);

				//Arrays.fill(literals, literalSize, literalSize + Constants.SIZE_OF_LONG, (byte) 0);
				for (int i = literalSize; i < literalSize + Constants.SIZE_OF_LONG; i++)
				{
					literals[i] = 0;
				}
			}
			else 
			{
				literalsBase = inputReader;
				literalsAddress = input;
				literalsLimit = literalsAddress + literalSize;
			}
			input += literalSize;

			return (int) (input - inputAddress);
		}

		static FrameHeader ReadFrameHeader(BinaryReader inputReader, long inputAddress, long inputLimit)
		{
			long input = inputAddress;
			Util.Verify(input < inputLimit, input, "Not enough input bytes");

			inputReader.BaseStream.Seek(input++, SeekOrigin.Begin);
			int frameHeaderDescriptor = inputReader.ReadByte() & 0xFF;
			bool singleSegment = (frameHeaderDescriptor & 0b100000) != 0;
			int dictionaryDescriptor = frameHeaderDescriptor & 0b11;
			int contentSizeDescriptor = Util.UnsignedRightShift(frameHeaderDescriptor, 6);

			int headerSize = 1 +
					(singleSegment ? 0 : 1) +
					(dictionaryDescriptor == 0 ? 0 : (1 << (dictionaryDescriptor - 1))) +
					(contentSizeDescriptor == 0 ? (singleSegment ? 1 : 0) : (1 << contentSizeDescriptor));

			Util.Verify(headerSize <= inputLimit - inputAddress, input, "Not enough input bytes");

			// decode window size
			int windowSize = -1;
			if (!singleSegment) 
			{
				inputReader.BaseStream.Seek(input++, SeekOrigin.Begin);
				int windowDescriptor = inputReader.ReadByte() & 0xFF;
				int exponent = Util.UnsignedRightShift(windowDescriptor, 3);
				int mantissa = windowDescriptor & 0b111;

				int fixedBase = 1 << (Constants.MIN_WINDOW_LOG + exponent);
				windowSize = fixedBase + (fixedBase / 8) * mantissa;
			}

			// decode dictionary id
			long dictionaryId = -1;
			switch (dictionaryDescriptor) 
			{
				case 1:
					inputReader.BaseStream.Seek(input, SeekOrigin.Begin);
					dictionaryId = inputReader.ReadByte() & 0xFF;
					input += Constants.SIZE_OF_BYTE;
					break;
				case 2:
					inputReader.BaseStream.Seek(input, SeekOrigin.Begin);
					dictionaryId = inputReader.ReadInt16() & 0xFFFF;
					input += Constants.SIZE_OF_SHORT;
					break;
				case 3:
					inputReader.BaseStream.Seek(input, SeekOrigin.Begin);
					dictionaryId = inputReader.ReadInt32() & 0xFFFFFFFF;
					input += Constants.SIZE_OF_INT;
					break;
			}
			Util.Verify(dictionaryId == -1, input, "Custom dictionaries not supported");

			// decode content size
			long contentSize = -1;
			switch (contentSizeDescriptor) 
			{
				case 0:
					if (singleSegment) 
					{
						inputReader.BaseStream.Seek(input, SeekOrigin.Begin);
						contentSize = inputReader.ReadByte() & 0xFF;
						input += Constants.SIZE_OF_BYTE;
					}
					break;
				case 1:
					inputReader.BaseStream.Seek(input, SeekOrigin.Begin);
					contentSize = inputReader.ReadInt16() & 0xFFFF;
					contentSize += 256;
					input += Constants.SIZE_OF_SHORT;
					break;
				case 2:
					inputReader.BaseStream.Seek(input, SeekOrigin.Begin);
					contentSize = inputReader.ReadInt32() & 0xFFFFFFFF;
					input += Constants.SIZE_OF_INT;
					break;
				case 3:
					inputReader.BaseStream.Seek(input, SeekOrigin.Begin);
					contentSize = inputReader.ReadInt64();
					input += Constants.SIZE_OF_LONG;
					break;
			}

			bool hasChecksum = (frameHeaderDescriptor & 0b100) != 0;

			return new FrameHeader(
					input - inputAddress,
					windowSize,
					contentSize,
					dictionaryId,
					hasChecksum);
		}

		public static long GetDecompressedSize(BinaryReader inputReader, long inputAddress, long inputLimit)
		{
			long input = inputAddress;
			input += VerifyMagic(inputReader, input, inputLimit);
			return ReadFrameHeader(inputReader, input, inputLimit).contentSize;
		}

		private static int VerifyMagic(BinaryReader inputReader, long inputAddress, long inputLimit)
		{
			Util.Verify(inputLimit - inputAddress >= 4, inputAddress, "Not enough input bytes");
			
			inputReader.BaseStream.Seek(inputAddress, SeekOrigin.Begin);
			uint magic = inputReader.ReadUInt32();
			if (magic != Constants.MAGIC_NUMBER) 
			{
				if (magic == V07_MAGIC_NUMBER) 
				{
					throw new Exception("Data encoded in unsupported ZSTD v0.7 format");
				}
				throw new Exception($"Invalid magic prefix: {magic.ToString("X")}");
			}

			return Constants.SIZE_OF_INT;
		}
	}
}