using pdj.tiny7z.Compress;
using System;
using System.Collections.Generic;

namespace pdj.tiny7z
{
    public static class z7Methods
    {
        public static readonly Dictionary<CodecID, string> Codecs;
        static z7Methods()
        {
            try
            {
                Codecs = new Dictionary<CodecID, string>()
                {
                    { new CodecID( 0x00 ), "Copy" },
                    { new CodecID( 0x03 ), "Delta" },
                    { new CodecID( 0x04 ), "BCJ (x86)" },
                    { new CodecID( 0x05 ), "PPC (big-endian)" },
                    { new CodecID( 0x06 ), "IA64" },
                    { new CodecID( 0x07 ), "ARM (little-endian)" },
                    { new CodecID( 0x08 ), "ARTM (little-endian)" },
                    { new CodecID( 0x09 ), "SPARC" },

                    { new CodecID( 0x21 ), "LZMA2" },

                    { new CodecID( 0x02, 0x03, 0x2 ), "Swap2" },
                    { new CodecID( 0x02, 0x03, 0x4 ), "Swap4" },

                    { new CodecID( 0x03, 0x01, 0x01 ), "7z LZMA" },

                    { new CodecID( 0x03, 0x03, 0x01, 0x03 ), "7z BCJ" },
                    { new CodecID( 0x03, 0x03, 0x01, 0x1B ), "7z BCJ2 (4 packed streams)" },
                    { new CodecID( 0x03, 0x03, 0x02, 0x05 ), "7z PPC (big-endian)" },
                    { new CodecID( 0x03, 0x03, 0x03, 0x01 ), "7z Alpha" },
                    { new CodecID( 0x03, 0x03, 0x04, 0x01 ), "7z IA64" },
                    { new CodecID( 0x03, 0x03, 0x05, 0x01 ), "7z ARM (little-endian)" },
                    { new CodecID( 0x03, 0x03, 0x06, 0x05 ), "7z M68 (big-endian)" },
                    { new CodecID( 0x03, 0x03, 0x07, 0x01 ), "7z ARMT (little-endian)" },
                    { new CodecID( 0x03, 0x03, 0x08, 0x05 ), "7z SPARC" },

                    { new CodecID( 0x03, 0x04, 0x01 ), "7z PPMD" },
                    { new CodecID( 0x03, 0x7F, 0x01 ), "7z Experimental Method" },

                    { new CodecID( 0x04, 0x00 ), "Reserved" },
                    { new CodecID( 0x04, 0x01, 0x00 ), "Zip Copy" },
                    { new CodecID( 0x04, 0x01, 0x01 ), "Zip Shrink" },
                    { new CodecID( 0x04, 0x01, 0x06 ), "Zip Implode" },
                    { new CodecID( 0x04, 0x01, 0x08 ), "Zip Deflate" },
                    { new CodecID( 0x04, 0x01, 0x09 ), "Zip Deflate64" },
                    { new CodecID( 0x04, 0x01, 0x0A ), "Zip Imploding" },
                    { new CodecID( 0x04, 0x01, 0x0C ), "Zip BZip2" },
                    { new CodecID( 0x04, 0x01, 0x0E ), "Zip LZMA (LZMA-zip)" },
                    { new CodecID( 0x04, 0x01, 0x5F ), "Zip xz" },
                    { new CodecID( 0x04, 0x01, 0x60 ), "Zip Jpeg" },
                    { new CodecID( 0x04, 0x01, 0x61 ), "Zip WavPack" },
                    { new CodecID( 0x04, 0x01, 0x62 ), "Zip PPMd (PPMd-zip)" },
                    { new CodecID( 0x04, 0x01, 0x63 ), "Zip wzAES" },

                    { new CodecID( 0x04, 0x03, 0x01 ), "Rar1" },
                    { new CodecID( 0x04, 0x03, 0x02 ), "Rar2" },
                    { new CodecID( 0x04, 0x03, 0x03 ), "Rar3" },
                    { new CodecID( 0x04, 0x03, 0x05 ), "Rar5" },

                    { new CodecID( 0x04, 0x04, 0x01 ), "Arj(1,2,3)" },
                    { new CodecID( 0x04, 0x04, 0x02 ), "Arj4" },

                    { new CodecID( 0x04, 0x05 ), "Z" },

                    { new CodecID( 0x04, 0x06 ), "Lzh" },

                    { new CodecID( 0x04, 0x07 ), "Reserved for 7z" },

                    { new CodecID( 0x04, 0x08 ), "Cab" },

                    { new CodecID( 0x04, 0x09, 0x01 ), "DeflateNSIS" },
                    { new CodecID( 0x04, 0x09, 0x02 ), "BZip2NSIS" },
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message + ex.StackTrace);
            }
        }
    }
}
