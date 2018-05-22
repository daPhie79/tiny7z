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
            long inSize,
            long outSize)
        {
            switch (method)
            {
                case Method.Copy:
                    if (properties != null || properties.Length > 0)
                        throw new NotSupportedException();
                    return inStreams.Single();
                case Method.AES:
                    return new AES.AesDecoderStream(inStreams.Single(), properties, password, outSize);
                case Method.BCJ:
                    return new BCJ.BcjFilter(false, inStreams.Single());
                case Method.BCJ2:
                    return new BCJ2.Bcj2DecoderStream(inStreams, properties, outSize);
                case Method.BZip2:
                    throw new NotSupportedException();
                case Method.Deflate:
                    throw new NotSupportedException();
                case Method.LZMA:
                case Method.LZMA2:
                    return new LZMA.LzmaStream(properties, inStreams.Single(), inSize, outSize);
                case Method.PPMd:
                    return new PPMd.PpmdStream(new PPMd.PpmdProperties(properties), inStreams.Single(), false);
            }
            return null;
        }
    }
}