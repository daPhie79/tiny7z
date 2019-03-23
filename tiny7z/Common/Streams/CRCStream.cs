using System;
using System.IO;

namespace pdj.tiny7z.Common
{
    /// <summary>
    /// Filter stream to calculate CRC32 on the fly.
    /// </summary>
    public class CRCStream : Stream
    {
        /// <summary>
        /// Access this once all stream has been read and it will be the stream's CRC32 value.
        /// </summary>
        public uint Result => this.crc.Result;

        public override bool CanRead => internalStream is Stream && internalStream.CanRead;
        public override bool CanWrite => internalStream is Stream && internalStream.CanWrite;
        public override bool CanSeek => false;
        public override long Length => internalStream is Stream ? internalStream.Length : -1;
        public override long Position
        {
            get => internalStream is Stream ? internalStream.Position : -1;
            set => throw new NotImplementedException();
        }

        public CRCStream()
            : base()
        {
            crc = new CRC();
            internalStream = null;
            leaveOpen = true;
        }

        public CRCStream(Stream internalStream, CRC crc, bool leaveOpen = true)
            : base()
        {
            this.crc = crc ?? new CRC();
            this.internalStream = internalStream;
            this.leaveOpen = leaveOpen;
        }

        public CRCStream(Stream internalStream, bool leaveOpen = true)
            : this(internalStream, null, leaveOpen)
        {
        }

        private CRC crc;
        private Stream internalStream;
        private bool leaveOpen;

        public override void Flush()
        {
            if (internalStream is Stream)
            {
                internalStream.Flush();
            }
        }

        public override void Close()
        {
            if (internalStream is Stream && !leaveOpen)
            {
                internalStream.Close();
            }
            internalStream = null;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int r = internalStream.Read(buffer, offset, count);
            if (r > 0)
                crc.Calculate(buffer, offset, r);
            return r;
        }

        public override int ReadByte()
        {
            int y = internalStream.ReadByte();
            if (y != -1)
                crc.Calculate((byte)y);
            return y;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (internalStream is Stream)
            {
                internalStream.Write(buffer, offset, count);
                crc.Calculate(buffer, offset, count);
            }
        }

        public override void WriteByte(byte value)
        {
            if (internalStream is Stream)
            {
                internalStream.WriteByte(value);
                crc.Calculate(value);
            }
        }
    }
}
