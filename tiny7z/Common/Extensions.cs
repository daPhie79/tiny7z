using System;
using System.Runtime.InteropServices;

namespace pdj.tiny7z.Common
{
    public static class Extensions
    {
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

        /// <summary>
        /// Extension to get a byte array out of a struct
        /// </summary>
        public static byte[] GetByteArray<T>(this T structObj) where T : struct
        {
            int structSize = Marshal.SizeOf(typeof(T));
            byte[] buffer = new byte[structSize];

            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            IntPtr pBuffer = handle.AddrOfPinnedObject();
            Marshal.StructureToPtr(structObj, pBuffer, false);
            handle.Free();

            return buffer;
        }

        /// <summary>
        /// Extension to get a struct out of a byte array
        /// </summary>
        public static T GetStruct<T>(this byte[] byteArray) where T : struct
        {
            int structSize = Marshal.SizeOf(typeof(T));
            if (byteArray.Length < structSize)
                throw new ArgumentOutOfRangeException();

            GCHandle handle = GCHandle.Alloc(byteArray, GCHandleType.Pinned);
            T structObj = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();

            return structObj;
        }
    }
}
