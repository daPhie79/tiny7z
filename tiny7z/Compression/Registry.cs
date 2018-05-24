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
        public static Stream GetDecoderStream(
            Method method,
            Stream[] inStreams,
            Byte[] properties,
            IPasswordProvider password,
            long limit)
        {
            switch (method)
            {
                case Method.Copy:
                    if (properties != null || properties.Length > 0)
                        throw new NotSupportedException();
                    return inStreams.Single();
                case Method.AES:
                    return new AesDecoderStream(inStreams.Single(), properties, password, limit);
                case Method.BCJ:
                    return new BcjDecoderStream(inStreams.Single(), properties, limit);
                case Method.BCJ2:
                    return new Bcj2DecoderStream(inStreams, properties, limit);
                case Method.LZMA:
                    return new LzmaDecoderStream(inStreams.Single(), properties, limit);
                case Method.LZMA2:
                    return new Lzma2DecoderStream(inStreams.Single(), properties.First(), limit);
                case Method.PPMd:
                    return new PpmdDecoderStream(inStreams.Single(), properties, limit);
                default:
                    throw new NotSupportedException();
            }
        }
    }
}
