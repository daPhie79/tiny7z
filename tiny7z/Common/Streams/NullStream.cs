using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pdj.tiny7z.Common
{
    class NullStream : Stream
    {
        long length;
        long pos;

        public NullStream(long length)
        {
            this.length = length;
            this.pos = 0;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => true;

        public override long Length => (long)length;

        public override long Position
        {
            get => pos;
            set
            {
                if (value > 0 && value < (long)length)
                    pos = value;
            }
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (pos + count > length)
                count = (int)(length - pos);
            pos += count;
            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    pos = offset;
                    break;
                case SeekOrigin.Current:
                    pos += offset;
                    break;
                case SeekOrigin.End:
                    pos = length - offset;
                    break;
            }
            if (pos < 0)
                pos = 0;
            if (pos > length)
                pos = length;
            return pos;
        }

        public override void SetLength(long value)
        {
            length = value;
            if (pos > length)
                pos = length;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (pos + count > length)
                count = (int)(length - pos);
            pos += count;
        }
    }
}
