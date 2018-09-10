using System.IO;

namespace pdj.tiny7z.Common
{
    public static class CRC
    {
        public static uint[] Table
        {
            get; private set;
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

        public static uint Calculate(byte data, uint crc = 0xffffffff)
        {
            byte index = (byte)(((crc) & 0xff) ^ data);
            return (uint)((crc >> 8) ^ Table[index]);
        }

        public static uint Calculate(byte[] data, int offset = 0, int count = -1, uint crc = 0xffffffff)
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
            return ~crc;
        }

        public static uint Calculate(Stream stream, uint crc = 0xffffffff)
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
            return ~crc;
        }
    }
}
