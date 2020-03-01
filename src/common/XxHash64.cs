using System;
using System.IO;

namespace CSharp_zstd
{
	public static class XxHash64
	{
		private const ulong PRIME64_1 = 0x9E3779B185EBCA87L;
		private const ulong PRIME64_2 = 0xC2B2AE3D27D4EB4FL;
		private const ulong PRIME64_3 = 0x165667B19E3779F9L;
		private const ulong PRIME64_4 = 0x85EBCA77C2b2AE63L;
		private const ulong PRIME64_5 = 0x27D4EB2F165667C5L;

		public static ulong Hash(ulong seed, BinaryReader inputReader, long address, int length)
		{
			ulong hash;
			if (length >= 32) 
			{
				hash = UpdateBody(seed, inputReader, address, length);
			}
			else 
			{
				hash = seed + PRIME64_5;
			}

			hash += (ulong) length;

			// round to the closest 32 byte boundary
			// this is the point up to which updateBody() processed
			int index = (int)(length & 0xFFFFFFE0);

			return UpdateTail(hash, inputReader, address, index, length);
		}

		private static ulong UpdateTail(ulong hash, BinaryReader inputReader, long address, int index, int length)
		{
			while (index <= length - 8) 
			{
				inputReader.BaseStream.Seek(address + index, SeekOrigin.Begin);
				hash = UpdateTail(hash, inputReader.ReadUInt64());
				index += 8;
			}

			if (index <= length - 4) 
			{
				inputReader.BaseStream.Seek(address + index, SeekOrigin.Begin);
				hash = UpdateTail(hash, inputReader.ReadInt32());
				index += 4;
			}

			while (index < length) 
			{
				inputReader.BaseStream.Seek(address + index, SeekOrigin.Begin);
				hash = UpdateTail(hash, inputReader.ReadByte());
				index++;
			}

			hash = FinalShuffle(hash);

			return hash;
		}

		private static ulong UpdateBody(ulong seed, BinaryReader inputReader, long address, int length)
		{
			ulong v1 = seed + PRIME64_1 + PRIME64_2;
			ulong v2 = seed + PRIME64_2;
			ulong v3 = seed;
			ulong v4 = seed - PRIME64_1;

			int remaining = length;
			while (remaining >= 32) 
			{
				inputReader.BaseStream.Seek(address, SeekOrigin.Begin);
				v1 = Mix(v1, inputReader.ReadUInt64());
				inputReader.BaseStream.Seek(address + 8, SeekOrigin.Begin);
				v2 = Mix(v2, inputReader.ReadUInt64());
				inputReader.BaseStream.Seek(address + 16, SeekOrigin.Begin);
				v3 = Mix(v3, inputReader.ReadUInt64());
				inputReader.BaseStream.Seek(address + 24, SeekOrigin.Begin);
				v4 = Mix(v4, inputReader.ReadUInt64());

				address += 32;
				remaining -= 32;
			}

			ulong hash = RotateLeft(v1, 1) + RotateLeft(v2, 7) + RotateLeft(v3, 12) + RotateLeft(v4, 18);

			hash = Update(hash, v1);
			hash = Update(hash, v2);
			hash = Update(hash, v3);
			hash = Update(hash, v4);

			return hash;
		}

		private static ulong Mix(ulong current, ulong value)
		{
			return RotateLeft(current + value * PRIME64_2, 31) * PRIME64_1;
		}

		private static ulong Update(ulong hash, ulong value)
		{
			ulong temp = hash ^ Mix(0, value);
			return temp * PRIME64_1 + PRIME64_4;
		}

		private static ulong UpdateTail(ulong hash, ulong value)
		{
			ulong temp = hash ^ Mix(0, value);
			return RotateLeft(temp, 27) * PRIME64_1 + PRIME64_4;
		}

		private static ulong UpdateTail(ulong hash, int value)
		{
			ulong unsigned = (ulong)(value & 0xFFFF_FFFFL);
			ulong temp = hash ^ (unsigned * PRIME64_1);
			return RotateLeft(temp, 23) * PRIME64_2 + PRIME64_3;
		}

		private static ulong UpdateTail(ulong hash, byte value)
		{
			int unsigned = value & 0xFF;
			ulong temp = hash ^ ((ulong)unsigned * PRIME64_5);
			return RotateLeft(temp, 11) * PRIME64_1;
		}

		private static ulong FinalShuffle(ulong hash)
		{
			hash ^= hash >> 33;
			hash *= PRIME64_2;
			hash ^= hash >> 29;
			hash *= PRIME64_3;
			hash ^= hash >> 32;
			return hash;
		}

		private static ulong RotateLeft(ulong value, int distance)
		{
			return (value << distance) | (value >> (64 - distance));
		}
	}
}