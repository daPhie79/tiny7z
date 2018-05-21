using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pdj.tiny7z.Common
{
    public static class Util
    {
        /// <summary>
        /// Alternative stream copy method to CopyTo.
        /// </summary>
        public static long TransferTo(this Stream source, Stream destination)
        {
            byte[] array = GetTransferByteArray();
            int count;
            long total = 0;
            while (ReadTransferBlock(source, array, out count))
            {
                total += count;
                destination.Write(array, 0, count);
            }
            return total;
        }

        /// <summary>
        /// Used by TransferTo
        /// </summary>
        private static bool ReadTransferBlock(Stream source, byte[] array, out int count)
        {
            return (count = source.Read(array, 0, array.Length)) != 0;
        }

        /// <summary>
        /// Used by TransferTo
        /// </summary>
        private static byte[] GetTransferByteArray()
        {
            return new byte[1024 * 1024];
        }

        /// <summary>
        /// Quick and dirty little-endian UInt16 get from a byte array.
        /// </summary>
        public static UInt16 GetLittleEndianUInt16(byte[] buffer, int offset)
        {
            return (UInt16)(buffer[offset]
                   + ((uint)buffer[offset + 1] << 8));
        }

        /// <summary>
        /// Quick and dirty little-endian UInt32 get from a byte array.
        /// </summary>
        public static UInt32 GetLittleEndianUInt32(byte[] buffer, int offset)
        {
            return buffer[offset]
                   + ((uint)buffer[offset + 1] << 8)
                   + ((uint)buffer[offset + 2] << 16)
                   + ((uint)buffer[offset + 3] << 24);
        }

        /// <summary>
        /// Quick and dirty little-endian UInt64 get from a byte array.
        /// </summary>
        public static UInt64 GetLittleEndianUInt64(byte[] buffer, int offset)
        {
            return buffer[offset]
                   + ((ulong)buffer[offset + 1] << 8)
                   + ((ulong)buffer[offset + 2] << 16)
                   + ((ulong)buffer[offset + 3] << 24)
                   + ((ulong)buffer[offset + 4] << 32)
                   + ((ulong)buffer[offset + 5] << 40)
                   + ((ulong)buffer[offset + 6] << 48)
                   + ((ulong)buffer[offset + 7] << 56);
        }
    }
}
