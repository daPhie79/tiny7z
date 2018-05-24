using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using LZMA = SevenZip.Compression.LZMA;

namespace pdj.tiny7z.Compression
{
    class LzmaDecoderStream : DecoderStream
    {
        private MemoryStream mBufferStream;
        private Stream mInputStream;
        private LZMA.Decoder mDecoder;
        private long mTotalRead;
        private long mLimit;

        public LzmaDecoderStream(Stream input, byte[] info, long limit)
        {
            mInputStream = input;
            mTotalRead = 0;
            mLimit = limit;
            mBufferStream = new MemoryStream();

            mDecoder = new LZMA.Decoder();
            mDecoder.SetDecoderProperties(info);
            mDecoder.Begin(mInputStream, mBufferStream, input.Length, limit);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (mBufferStream != null)
                    mBufferStream.Dispose();
                if (mDecoder != null)
                    mDecoder.Cleanup();
                mBufferStream = null;
                mInputStream = null;
                mDecoder = null;
            }
        }

        public override long Length
        {
            get { return mLimit; }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");
            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException("offset");
            if (count < 0 || count > buffer.Length - offset)
                throw new ArgumentOutOfRangeException("count");
            if (count == 0 || mTotalRead == mLimit)
                return 0;

            if (count > mLimit - mTotalRead)
                count = checked((int)(mLimit - mTotalRead));

            int read = 0;
            while (count > 0)
            {
                int r = mBufferStream.Read(buffer, offset, count);
                if (r > 0)
                {
                    read += r;
                    offset += r;
                    count -= r;
                    mTotalRead += r;
                }
                else
                {
                    mBufferStream.Position = 0;
                    mBufferStream.SetLength(0);
                    mDecoder.Code(4 << 16);
                    if (mBufferStream.Length == 0)
                        throw new EndOfStreamException("Decoded data is smaller than expected.");
                    mBufferStream.Position = 0;
                }
            }

            return read;
        }

    }
}
