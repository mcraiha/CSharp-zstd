using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using CSharp_zstd;

namespace tests
{
	public class ZstdTests
	{
		[SetUp]
		public void Setup()
		{
		}

		[Test]
		public void DecompressWithOutputPaddingAndChecksumTest()
		{
			// Arrange
			int padding = 1021;

			byte[] compressed = File.ReadAllBytes(Path.Combine(TestContext.CurrentContext.TestDirectory,"testfiles/with-checksum.zst"));
			byte[] uncompressed = File.ReadAllBytes(Path.Combine(TestContext.CurrentContext.TestDirectory,"testfiles/with-checksum.txt"));

			byte[] output = new byte[uncompressed.Length + padding * 2]; // pre + post padding

			ZstdDecompressor decompressor = new ZstdDecompressor();

			// Act
			int decompressedSize = decompressor.Decompress(compressed, 0, compressed.Length, output, padding, output.Length);
			byte[] outputPaddingRemoved = output.Skip(padding).Take(decompressedSize).ToArray();

			// Assert
			Assert.That(decompressedSize, Is.EqualTo(uncompressed.Length));
			Assert.That(outputPaddingRemoved, Is.EqualTo(uncompressed));
		}

		[Test]
		public void DecompressSampleFileTest()
		{
			// Arrange
			byte[] compressed = File.ReadAllBytes(Path.Combine(TestContext.CurrentContext.TestDirectory,"testfiles/z000107.zst"));
			byte[] uncompressed = File.ReadAllBytes(Path.Combine(TestContext.CurrentContext.TestDirectory,"testfiles/z000107"));

			byte[] output = new byte[uncompressed.Length]; // pre + post padding

			ZstdDecompressor decompressor = new ZstdDecompressor();

			// Act
			int decompressedSize = decompressor.Decompress(compressed, 0, compressed.Length, output, 0, output.Length);

			// Assert
			Assert.That(decompressedSize, Is.EqualTo(uncompressed.Length));
			Assert.That(output, Is.EqualTo(uncompressed));
		}
	}
}