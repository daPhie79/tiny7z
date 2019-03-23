using System;
using System.Diagnostics;
using System.IO;

namespace pdj.tiny7z.Common
{
    /// <summary>
    /// Stream that restricts access from another stream to specific boundaries.
    /// </summary>
    public class SubStream : Stream
    {
        public override bool CanRead => internalStream is Stream && internalStream.CanRead;
        public override bool CanSeek => internalStream is Stream && internalStream.CanSeek;
        public override bool CanWrite => internalStream is Stream && internalStream.CanWrite;
        public override bool CanTimeout => internalStream is Stream && internalStream.CanTimeout;
        public override long Length => windowSize;
        public override long Position
        {
            get => currentOffset - startOffset;
            set
            {
                if (value < 0 || value > windowSize)
                    throw new ArgumentOutOfRangeException(nameof(value));
                currentOffset = startOffset + value;
            }
        }

        public SubStream()
            : base()
        {
        }

        public SubStream(Stream stream)
            : base()
        {
            this.internalStream = stream;
            this.startOffset = 0;
            this.windowSize = stream.Length;
            this.currentOffset = stream.Position;
        }

        public SubStream(Stream stream, long startOffset, long windowSize)
            : base()
        {
            if (startOffset < 0 || startOffset > stream.Length)
                throw new ArgumentOutOfRangeException(nameof(startOffset));
            if (windowSize <= 0 || windowSize + startOffset > stream.Length)
                throw new ArgumentOutOfRangeException(nameof(windowSize));

            this.internalStream = stream;
            this.startOffset = startOffset;
            this.windowSize = windowSize;
            this.currentOffset = startOffset;
        }

        private Stream internalStream;
        private long startOffset;
        private long windowSize;
        private long currentOffset;

        public override void Write(byte[] array, int offset, int count)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));
            if (offset < 0 || offset >= array.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count <= 0 || count + offset > array.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            if (internalStream is Stream)
            {
                if (internalStream.Position != currentOffset)
                    internalStream.Position = currentOffset;
                if (currentOffset + count > startOffset + windowSize)
                {
                    int newCount = (int)((startOffset + windowSize) - currentOffset);
                    Trace.TraceWarning($"End of substream window reached, {newCount} of {count} bytes written.");

                    count = newCount;
                }
                if (count > 0)
                {
                    internalStream.Write(array, offset, count);
                    currentOffset += count;
                }
            }
        }

        public override void WriteByte(byte value)
        {
            if (internalStream is Stream)
            {
                if (internalStream.Position != currentOffset)
                    internalStream.Position = currentOffset;
                if (currentOffset < startOffset + windowSize)
                {
                    internalStream.WriteByte(value);
                    ++currentOffset;
                }
                else
                    Trace.TraceWarning("End of substream window reached, one byte was not written.");
            }
        }

        public override int Read(byte[] array, int offset, int count)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));
            if (offset < 0 || offset >= array.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count <= 0 || count + offset > array.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            int r = 0;
            if (internalStream is Stream)
            {
                if (internalStream.Position != currentOffset)
                    internalStream.Position = currentOffset;
                if (currentOffset + count > startOffset + windowSize)
                    count = (int)((startOffset + windowSize) - currentOffset);
                if (count > 0)
                {
                    r = internalStream.Read(array, offset, count);
                    currentOffset += count;
                }
            }
            return r;
        }

        public override int ReadByte()
        {
            int r = -1;
            if (internalStream is Stream)
            {
                if (internalStream.Position != currentOffset)
                    internalStream.Position = currentOffset;
                if (currentOffset < startOffset + windowSize)
                {
                    r = internalStream.ReadByte();
                    ++currentOffset;
                }
            }
            return r;
        }

        public override void Flush()
        {
            if (internalStream is Stream)
            {
                internalStream.Flush();
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override int ReadTimeout { get => 0; }

        public override int WriteTimeout { get => 0; }
    }
}
