using pdj.tiny7z.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace pdj.tiny7z.Archive
{
    public static class SevenZipMethods
    {
        public class MethodID : IEquatable<MethodID>
        {
            public MethodID() : this(new byte[0]) { }
            public MethodID(params byte[] id)
            {
                Raw = id.ToArray();
            }

            public int Size
            {
                get => Raw.Length;
            }

            public byte[] Raw
            {
                get;
            }

            public static bool operator ==(MethodID c1, MethodID c2)
            {
                if (ReferenceEquals(c1, null) && ReferenceEquals(c2, null))
                    return true;
                if (ReferenceEquals(c1, null) || ReferenceEquals(c2, null))
                    return false;
                return c1.Equals(c2);
            }

            public static bool operator !=(MethodID c1, MethodID c2)
            {
                return !(c1 == c2);
            }

            public override bool Equals(object obj)
            {
                return !ReferenceEquals(obj, null) && Equals(obj as MethodID);
            }

            public bool Equals(MethodID otherMethodID)
            {
                if (otherMethodID.Raw.Length != Raw.Length)
                    return false;
                return Raw.SequenceEqual(otherMethodID.Raw);
            }

            public override int GetHashCode()
            {
                return computeHash(Raw);
            }

            private static int computeHash(params byte[] data)
            {
                unchecked
                {
                    const int p = 16777619;
                    int hash = (int)2166136261;

                    for (int i = 0; i < data.Length; i++)
                        hash = (hash ^ data[i]) * p;

                    hash += hash << 13;
                    hash ^= hash >> 7;
                    hash += hash << 3;
                    hash ^= hash >> 17;
                    hash += hash << 5;
                    return hash;
                }
            }
        }

        public static readonly Dictionary<MethodID, string> List;
        public static readonly Dictionary<MethodID, Compression.Registry.Method> Supported;
        public static readonly Dictionary<Compression.Registry.Method, MethodID> Lookup;

        static SevenZipMethods()
        {
            try
            {
                List = new Dictionary<MethodID, string>
                {
                    { new MethodID( 0x00 ), "Copy" },
                    { new MethodID( 0x03 ), "Delta" },
                    { new MethodID( 0x04 ), "BCJ (x86)" },
                    { new MethodID( 0x05 ), "PPC (big-endian)" },
                    { new MethodID( 0x06 ), "IA64" },
                    { new MethodID( 0x07 ), "ARM (little-endian)" },
                    { new MethodID( 0x08 ), "ARTM (little-endian)" },
                    { new MethodID( 0x09 ), "SPARC" },

                    { new MethodID( 0x21 ), "LZMA2" },

                    { new MethodID( 0x02, 0x03, 0x2 ), "Swap2" },
                    { new MethodID( 0x02, 0x03, 0x4 ), "Swap4" },

                    { new MethodID( 0x03, 0x01, 0x01 ), "7z LZMA" },

                    { new MethodID( 0x03, 0x03, 0x01, 0x03 ), "7z BCJ" },
                    { new MethodID( 0x03, 0x03, 0x01, 0x1B ), "7z BCJ2 (4 packed streams)" },
                    { new MethodID( 0x03, 0x03, 0x02, 0x05 ), "7z PPC (big-endian)" },
                    { new MethodID( 0x03, 0x03, 0x03, 0x01 ), "7z Alpha" },
                    { new MethodID( 0x03, 0x03, 0x04, 0x01 ), "7z IA64" },
                    { new MethodID( 0x03, 0x03, 0x05, 0x01 ), "7z ARM (little-endian)" },
                    { new MethodID( 0x03, 0x03, 0x06, 0x05 ), "7z M68 (big-endian)" },
                    { new MethodID( 0x03, 0x03, 0x07, 0x01 ), "7z ARMT (little-endian)" },
                    { new MethodID( 0x03, 0x03, 0x08, 0x05 ), "7z SPARC" },

                    { new MethodID( 0x03, 0x04, 0x01 ), "7z PPMD" },
                    { new MethodID( 0x03, 0x7F, 0x01 ), "7z Experimental Method" },

                    { new MethodID( 0x04, 0x00 ), "Reserved" },
                    { new MethodID( 0x04, 0x01, 0x00 ), "Zip Copy" },
                    { new MethodID( 0x04, 0x01, 0x01 ), "Zip Shrink" },
                    { new MethodID( 0x04, 0x01, 0x06 ), "Zip Implode" },
                    { new MethodID( 0x04, 0x01, 0x08 ), "Zip Deflate" },
                    { new MethodID( 0x04, 0x01, 0x09 ), "Zip Deflate64" },
                    { new MethodID( 0x04, 0x01, 0x0A ), "Zip Imploding" },
                    { new MethodID( 0x04, 0x01, 0x0C ), "Zip BZip2" },
                    { new MethodID( 0x04, 0x01, 0x0E ), "Zip LZMA (LZMA-zip)" },
                    { new MethodID( 0x04, 0x01, 0x5F ), "Zip xz" },
                    { new MethodID( 0x04, 0x01, 0x60 ), "Zip Jpeg" },
                    { new MethodID( 0x04, 0x01, 0x61 ), "Zip WavPack" },
                    { new MethodID( 0x04, 0x01, 0x62 ), "Zip PPMd (PPMd-zip)" },
                    { new MethodID( 0x04, 0x01, 0x63 ), "Zip wzAES" },

                    { new MethodID( 0x04, 0x02, 0x02 ), "BZip2" },

                    { new MethodID( 0x04, 0x03, 0x01 ), "Rar1" },
                    { new MethodID( 0x04, 0x03, 0x02 ), "Rar2" },
                    { new MethodID( 0x04, 0x03, 0x03 ), "Rar3" },
                    { new MethodID( 0x04, 0x03, 0x05 ), "Rar5" },

                    { new MethodID( 0x04, 0x04, 0x01 ), "Arj(1,2,3)" },
                    { new MethodID( 0x04, 0x04, 0x02 ), "Arj4" },

                    { new MethodID( 0x04, 0x05 ), "Z" },

                    { new MethodID( 0x04, 0x06 ), "Lzh" },

                    { new MethodID( 0x04, 0x07 ), "Reserved for 7z" },

                    { new MethodID( 0x04, 0x08 ), "Cab" },

                    { new MethodID( 0x04, 0x09, 0x01 ), "DeflateNSIS" },
                    { new MethodID( 0x04, 0x09, 0x02 ), "BZip2NSIS" },

                    { new MethodID( 0x06, 0xF1, 0x07, 0x01 ), "7zAES (AES-256 + SHA-256)" },
                };

                Supported = new Dictionary<MethodID, Compression.Registry.Method>
                {
                    { new MethodID( 0x00 ), Compression.Registry.Method.Copy },
                    { new MethodID( 0x21 ), Compression.Registry.Method.LZMA2 },
                    { new MethodID( 0x03, 0x01, 0x01 ), Compression.Registry.Method.LZMA },
                    { new MethodID( 0x03, 0x03, 0x01, 0x03 ), Compression.Registry.Method.BCJ },
                    { new MethodID( 0x03, 0x03, 0x01, 0x1B ), Compression.Registry.Method.BCJ2 },
                    { new MethodID( 0x03, 0x04, 0x01 ), Compression.Registry.Method.PPMd },
                    { new MethodID( 0x04, 0x01, 0x08 ), Compression.Registry.Method.Deflate },
                    { new MethodID( 0x04, 0x02, 0x02 ), Compression.Registry.Method.BZip2 },
                    { new MethodID( 0x06, 0xF1, 0x07, 0x01 ), Compression.Registry.Method.AES },
                };

                Lookup = Supported.ToDictionary(kp => kp.Value, kp => kp.Key);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message + ex.StackTrace);
            }
        }
    }
}
