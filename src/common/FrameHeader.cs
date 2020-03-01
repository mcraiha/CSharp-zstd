using System;

namespace CSharp_zstd
{
	public class FrameHeader
	{
		public readonly long headerSize;
		public readonly int windowSize;
		public readonly long contentSize;
		public readonly long dictionaryId;
		public readonly bool hasChecksum;

		public FrameHeader(long headerSize, int windowSize, long contentSize, long dictionaryId, bool hasChecksum)
		{
			this.headerSize = headerSize;
			this.windowSize = windowSize;
			this.contentSize = contentSize;
			this.dictionaryId = dictionaryId;
			this.hasChecksum = hasChecksum;
		}

		public override bool Equals(object value)
		{
			if (this == value) 
			{
				return true;
			}

			if (value == null || this.GetType() != value.GetType()) 
			{
				return false;
			}

			FrameHeader that = (FrameHeader) value;

			return headerSize == that.headerSize &&
					windowSize == that.windowSize &&
					contentSize == that.contentSize &&
					dictionaryId == that.dictionaryId &&
					hasChecksum == that.hasChecksum;
		}

		public override int GetHashCode()
		{
			return (headerSize, windowSize, contentSize, dictionaryId, hasChecksum).GetHashCode();
			//return HashCode.Combine(headerSize, windowSize, contentSize, dictionaryId, hasChecksum);
		}

		public override string ToString()
		{
			return string.Join(", ", new string[] {this.GetType().ToString(), "headerSize=" + headerSize, "windowSize=" + windowSize, "contentSize=" + contentSize, "dictionaryId=" + dictionaryId, "hasChecksum=" + hasChecksum });
		}
	}
}