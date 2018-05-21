using System;
using System.Runtime.InteropServices;

namespace pdj.tiny7z.Common
{
    public static class Extensions
    {
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
