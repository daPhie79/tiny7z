using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pdj.tiny7z.Common
{
    public class SubStream : Stream
    {
        private Stream internalStream;
        private long startOffset;
        private long windowSize;

        public override bool CanRead => internalStream.CanRead;

        public override bool CanSeek => internalStream.CanSeek;

        public override bool CanWrite => internalStream.CanWrite;

        public override long Length => windowSize;

        public override long Position
        {
            get => internalStream.Position - startOffset;
            set => internalStream.Position = ((value > windowSize) ? windowSize : value) + startOffset;
        }

        public SubStream() : base() { }

        public SubStream(Stream stream) : base()
        {
            this.internalStream = stream;
            this.startOffset = 0;
            this.windowSize = stream.Length;
        }

        public SubStream(Stream stream, long startOffset, long windowSize)
            : base()
        {
            this.internalStream = stream;
            this.startOffset = startOffset;
            this.windowSize = windowSize;
        }

        public override void Write(byte[] array, int offset, int count)
        {
            if (internalStream is Stream)
            {
                long currentPosition = internalStream.Position;
                if (currentPosition + count > startOffset + windowSize)
                {
                    count = (int)((startOffset + windowSize) - currentPosition);
                }
                if (count > 0)
                {
                    internalStream.Write(array, offset, count);
                }
            }
        }

        public override void WriteByte(byte value)
        {
            if (internalStream is Stream)
            {
                if (internalStream.Position < startOffset + windowSize)
                    internalStream.WriteByte(value);
            }
        }

        public override int Read(byte[] array, int offset, int count)
        {
            int r = 0;
            if (internalStream is Stream)
            {
                long currentPosition = internalStream.Position;
                if (currentPosition + count > startOffset + windowSize)
                {
                    count = (int)((startOffset + windowSize) - currentPosition);
                }
                if (count > 0)
                {
                    r = internalStream.Read(array, offset, count);
                }
            }
            return r;
        }

        public override int ReadByte()
        {
            int r = -1;
            if (internalStream is Stream)
            {
                if (internalStream.Position < startOffset + windowSize)
                    r = internalStream.ReadByte();
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
    }
}
