using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pdj.tiny7z.Common
{
    /// <summary>
    /// MultiFileStream - Allows treating a bunch of files sequentially to behave as if they're one stream.
    /// </summary>
    public class MultiFileStream : Stream
    {
        public string[] Names
        {
            get; private set;
        }

        public UInt64[] Sizes
        {
            get; private set;
        }

        public UInt32[] CRCs
        {
            get; private set;
        }

        private CRCStream internalStream;
        private long currentIndex;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => Sizes.Sum(size => (long)size);

        public override long Position
        {
            get
            {
                UInt64 startOffset = 0;
                long i = 0;
                foreach (var fileSize in Sizes)
                    if (i++ < currentIndex)
                        startOffset += fileSize;
                return (long)startOffset + (internalStream != null ? internalStream.Position : 0);
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public MultiFileStream()
            : base()
        {
            Names = new string[0];
            Sizes = new UInt64[0];
            CRCs = new UInt32[0];
            internalStream = null;
            currentIndex = 0;
        }

        public MultiFileStream(params string[] fileNames)
            : base()
        {
            if (fileNames == null || fileNames.Length == 0)
                throw new ArgumentOutOfRangeException();

            this.Names = fileNames;
            this.Sizes = new UInt64[fileNames.LongLength];
            this.CRCs = new UInt32[fileNames.LongLength];
            for (long i = 0; i < fileNames.LongLength; ++i)
                this.Sizes[i] = (ulong)new FileInfo(fileNames[i]).Length;

            internalStream = new CRCStream(File.OpenRead(fileNames[0]));
            currentIndex = 0;
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
                        CRCs[currentIndex] = internalStream.CRC;
                        internalStream.Dispose();
                        if (++currentIndex < Names.LongLength)
                        {
                            internalStream = new CRCStream(File.OpenRead(Names[currentIndex]));
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
                if(r == -1)
                {
                    this.CRCs[currentIndex] = internalStream.CRC;
                    internalStream.Dispose();
                    if (++currentIndex < Names.LongCount())
                    {
                        internalStream = new CRCStream(File.OpenRead(Names[(int)currentIndex]));
                        return ReadByte();
                    }
                    else
                    {
                        internalStream = null;
                    }
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

