using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pdj.tiny7z.Common
{
    /// <summary>
    /// MultiStream - Allows a multiple number of streams to be treated as one. Stream obtained (and get ownership) from delegate.
    /// </summary>
    public class MultiStream : Stream
    {
        public UInt64[] Sizes
        {
            get; private set;
        }

        public UInt32[] CRCs
        {
            get; private set;
        }

        private Func<ulong, Stream> onNextStream;
        private CRCStream internalStream;
        private long maxStreams;
        private long currentPos;
        private long currentSize;
        private long currentIndex;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotImplementedException();

        public override long Position
        {
            get => currentPos;
            set => throw new NotImplementedException();
        }

        public MultiStream()
            : base()
        {
            Sizes = new UInt64[0];
            CRCs = new UInt32[0];

            internalStream = null;
            maxStreams = 0;
            currentPos = 0;
            currentSize = 0;
            currentIndex = 0;
        }

        public MultiStream(long maxStreams, Func<ulong, Stream> onNextStream)
            : base()
        {
            if (maxStreams == 0 || onNextStream == null)
                throw new ArgumentOutOfRangeException();

            Sizes = new UInt64[maxStreams];
            CRCs = new UInt32[maxStreams];

            this.onNextStream = onNextStream;
            this.maxStreams = maxStreams;
            currentPos = 0;
            currentSize = 0;
            currentIndex = 0;

            this.internalStream = new CRCStream(onNextStream((ulong)currentIndex));
        }

        public override void Write(byte[] array, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override void WriteByte(byte value)
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] array, int offset, int count)
        {
            int read = 0;
            if (internalStream is Stream)
            {
                while (count > 0)
                {
                    int r = internalStream.Read(array, offset, count);
                    if (r == 0)
                    {
                        Sizes[currentIndex] = (UInt64)currentSize;
                        CRCs[currentIndex] = internalStream.CRC;
                        internalStream.Dispose();
                        currentSize = 0;
                        if (++currentIndex < maxStreams)
                        {
                            internalStream = new CRCStream(onNextStream((ulong)currentIndex));
                        }
                        else
                        {
                            internalStream = null;
                            break;
                        }
                    }
                    else
                    {
                        offset += r;
                        read += r;
                        count -= r;
                        currentPos += r;
                        currentSize += r;
                    }
                }
            }
            return read;
        }

        public override int ReadByte()
        {
            int r = -1;
            if (internalStream is Stream)
            {
                r = internalStream.ReadByte();
                if (r == -1)
                {
                    Sizes[currentIndex] = (UInt64)currentSize;
                    CRCs[currentIndex] = internalStream.CRC;
                    internalStream.Dispose();
                    currentSize = 0;
                    if (++currentIndex < maxStreams)
                    {
                        internalStream = new CRCStream(onNextStream((ulong)currentIndex));
                        return ReadByte();
                    }
                    else
                    {
                        internalStream = null;
                    }
                }
                else
                {
                    ++currentPos;
                    ++currentSize;
                }
            }
            return r;
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Close()
        {
            if (internalStream is Stream)
            {
                internalStream.Dispose();
                internalStream = null;
            }
        }
    }
}
