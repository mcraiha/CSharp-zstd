using NUnit.Framework;
using CSharp_zstd;
using System.IO;

namespace tests
{
	public class XxHash64Tests
	{
		private static readonly ulong PRIME = 2654435761L;

    	private readonly byte[] buffer = new byte[101];

		[SetUp]
		public void Setup()
		{
			ulong value = PRIME;
			for (int i = 0; i < buffer.Length; i++) 
			{
				buffer[i] = (byte) (value >> 24);
				value *= value;
			}
		}

		[Test]
		public void SanityTest()
		{
			// Arrange

			// Act
			ulong hash1 = XxHash64.Hash(0, new BinaryReader( new MemoryStream(buffer)), 0, 0);
			ulong hash2 = XxHash64.Hash(0, new BinaryReader( new MemoryStream(buffer)), 0, 1);
			ulong hash3 = XxHash64.Hash(PRIME, new BinaryReader( new MemoryStream(buffer)), 0, 1);
			ulong hash4 = XxHash64.Hash(0, new BinaryReader( new MemoryStream(buffer)), 0, 4);
			ulong hash5 = XxHash64.Hash(PRIME, new BinaryReader( new MemoryStream(buffer)), 0, 4);

			// Assert
			Assert.That(hash1, Is.EqualTo(0xEF46DB3751D8E999));
			Assert.That(hash2, Is.EqualTo(0x4FCE394CC88952D8L));
			Assert.That(hash3, Is.EqualTo(0x739840CB819FA723L));
			Assert.That(hash4, Is.EqualTo(0x9256E58AA397AEF1L));
			Assert.That(hash5, Is.EqualTo(0x9D5FFDFB928AB4BL));
		}
	}
}