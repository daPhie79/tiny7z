using System.IO;

namespace pdj.tiny7z.Common
{
    /// <summary>
    /// CRC calculations helper class
    /// </summary>
    public class CRC
    {
        public static uint[] Table
        {
            get; private set;
        }

        public uint Result
        {
            get => ~crc;
        }

        public CRC(uint crc = 0xffffffff)
        {
            this.crc = crc;
        }

        private uint crc;

        public CRC Calculate(byte data)
        {
            byte index = (byte)(((crc) & 0xff) ^ data);
            crc = (uint)((crc >> 8) ^ Table[index]);
            return this;
        }

        public CRC Calculate(byte[] data, int offset = 0, int count = -1)
        {
            if (count == -1)
                count = data.Length - offset;

            if (count > 0)
            {
                for (int i = 0; i < count; ++i)
                {
                    byte index = (byte)(((crc) & 0xff) ^ data[offset + i]);
                    crc = (uint)((crc >> 8) ^ Table[index]);
                }
            }
            return this;
        }

        public CRC Calculate(Stream stream)
        {
            int bufferSize = 1048576;
            byte[] buffer = new byte[bufferSize];

            long r = 0;
            while ((r = stream.Read(buffer, 0, bufferSize)) > 0)
            {
                for (int i = 0; i < r; ++i)
                {
                    byte index = (byte)(((crc) & 0xff) ^ buffer[i]);
                    crc = (uint)((crc >> 8) ^ Table[index]);
                }
            }
            return this;
        }

        static CRC()
        {
            Table = new uint[256];

            uint poly = 0xEDB88320;
            uint temp = 0;
            for (uint i = 0; i < Table.Length; ++i)
            {
                temp = i;
                for (int j = 8; j > 0; --j)
                {
                    if ((temp & 1) == 1)
                    {
                        temp = (uint)((temp >> 1) ^ poly);
                    }
                    else
                    {
                        temp >>= 1;
                    }
                }
                Table[i] = temp;
            }
        }
    }
}
