using System;
using System.IO;

namespace pdj.tiny7z.Common
{
    public static class Util
    {
        /// <summary>
        /// Alternative stream copy method to CopyTo. Stops writing when count is reached.
        /// </summary>
        public static long TransferTo(this Stream source, Stream destination, long count, Archive.IProgressProvider progress = null)
        {
            if (count == 0)
                throw new ArgumentOutOfRangeException();

            byte[] array = getTransferByteArray();
            long total = 0;
            while (count > 0)
            {
                int next = array.Length;
                if (next > count)
                    next = (int)count;

                int r = source.Read(array, 0, next);
                if (r == 0)
                    break;

                total += r;
                count -= r;
                destination.Write(array, 0, r);

                if (progress != null)
                    progress.SetProgress((ulong)total, 0);
            }
            return total;
        }

        /// <summary>
        /// Alternative stream copy method to CopyTo. Stops writing when no more data is available from input.
        /// </summary>
        public static long TransferTo(this Stream source, Stream destination, Archive.IProgressProvider progress = null)
        {
            byte[] array = getTransferByteArray();
            int count;
            long total = 0;
            while ((count = source.Read(array, 0, array.Length)) > 0)
            {
                destination.Write(array, 0, count);
                total += count;

                if (progress != null)
                    progress.SetProgress((ulong)total, 0);
            }
            return total;
        }

        /// <summary>
        /// Returns a write buffer for TransferTo
        /// </summary>
        private static byte[] getTransferByteArray()
        {
            return new byte[4 << 14];
        }

        /// <summary>
        /// Performs an unsigned bitwise right shift with the specified number
        /// </summary>
        public static int URShift(int number, int bits)
        {
            if (number >= 0)
            {
                return number >> bits;
            }
            return (number >> bits) + (2 << ~bits);
        }

        /// <summary>
        /// Performs an unsigned bitwise right shift with the specified number
        /// </summary>
        public static long URShift(long number, int bits)
        {
            if (number >= 0)
            {
                return number >> bits;
            }
            return (number >> bits) + (2L << ~bits);
        }
    }
}
