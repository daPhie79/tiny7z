using System;
using System.IO;
using System.Linq;

namespace pdj.tiny7z.Compression
{
    public static class Registry
    {
        /// <summary>
        /// List of supported coding/decoding methods.
        /// </summary>
        public enum Method
        {
            Copy,
            AES,
            BCJ,
            BCJ2,
            BZip2,
            Deflate,
            LZMA,
            LZMA2,
            PPMd,
        }

        /// <summary>
        /// Creates a stream of a specific decoder type.
        /// </summary>
        /// <param name="method">Decoder type.</param>
        /// <param name="inStreams">One or more streams needed to feed to the decoder.</param>
        /// <param name="properties">Array of bytes containing properties, if required by the decoder.</param>
        /// <param name="pw">Password, if needed.</param>
        /// <param name="limit">Size limit.</param>
        /// <returns></returns>
        public static Stream GetDecoderStream(
            Method method,
            Stream[] inStreams,
            Byte[] properties,
            IPasswordProvider pw,
            long limit)
        {
            switch (method)
            {
                case Method.Copy:
                    if (properties != null || properties.Length > 0)
                        throw new NotSupportedException();
                    return inStreams.Single();
                case Method.AES:
                    throw new NotSupportedException();
                case Method.BCJ:
                    break;
                case Method.BCJ2:
                    break;
                case Method.BZip2:
                    throw new NotSupportedException();
                case Method.Deflate:
                    throw new NotSupportedException();
                case Method.LZMA:
                    break;
                case Method.LZMA2:
                    break;
                case Method.PPMd:
                    break;
            }
            return null;
        }
    }
}