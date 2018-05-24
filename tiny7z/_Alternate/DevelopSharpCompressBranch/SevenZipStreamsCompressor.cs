using pdj.tiny7z.Common;
using System;
using System.IO;
using System.Linq;

namespace pdj.tiny7z.Archive
{
    class z7StreamsCompressor
    {
        public class PackedStream
        {
            public UInt64 NumStreams;
            public UInt64[] Sizes;
            public UInt32?[] CRCs;
            public SevenZipHeader.Folder Folder;
        }

        public Compression.Registry.Method? Method
        {
            get; set;
        }

        Stream stream;

        public z7StreamsCompressor(Stream stream)
        {
            this.stream = stream;
            Method = Compression.Registry.Method.LZMA;
        }

        public PackedStream Compress(Stream inputStream)
        {
            // Compression method
            if (!Method.HasValue || !SevenZipMethods.Lookup.ContainsKey(Method.Value))
                throw new SevenZipException("Undefined compression method.");
            var MethodID = SevenZipMethods.Lookup[Method.Value];

            // create compressed stream information structure
            var ps = new PackedStream()
            {
                NumStreams = 1,
                Sizes = new UInt64[1] { 0 },
                CRCs = new UInt32?[1] { null },
                Folder = new SevenZipHeader.Folder()
                {
                    NumCoders = 1,
                    CodersInfo = new SevenZipHeader.CoderInfo[1]
                    {
                        new SevenZipHeader.CoderInfo()
                        {
                            Attributes = (Byte)MethodID.Raw.Length,
                            CodecId = MethodID.Raw.ToArray(),
                            NumInStreams = 1,
                            NumOutStreams = 1
                        }
                    },
                    NumInStreamsTotal = 1,
                    NumOutStreamsTotal = 1,
                    PackedIndices = new UInt64[1] { 0 },
                    UnPackSizes = new UInt64[1] { 0 },
                    UnPackCRC = 0
                }
            };

            // remember current offsets
            long outStreamStartOffset = this.stream.Position;
            long inStreamStartOffset = inputStream.Position;

            // encode while calculating CRCs
            using (var inCRCStream = new CRCStream(inputStream))
            using (var outCRCStream = new CRCStream(stream))
            {
                // get and setup compressor
                using (var encoder = new Compression.LZMA.LzmaStream(new Compression.LZMA.LzmaEncoderProperties(), false, outCRCStream))
                {
                    // keep settings in header
                    var properties = encoder.Properties;
                    ps.Folder.CodersInfo[0].Attributes |= (Byte)SevenZipHeader.CoderInfo.AttrHasAttributes;
                    ps.Folder.CodersInfo[0].Properties = properties.ToArray();
                    ps.Folder.CodersInfo[0].PropertiesSize = (UInt64)ps.Folder.CodersInfo[0].Properties.Length;

                    // go!
                    Util.TransferTo(inCRCStream, encoder);
                }

                // store sizes and checksums
                ps.Folder.UnPackSizes[0] = (UInt64)(inputStream.Position - inStreamStartOffset);
                ps.Folder.UnPackCRC = inCRCStream.CRC;
                ps.Sizes[0] = (UInt64)(this.stream.Position - outStreamStartOffset);
                ps.CRCs[0] = outCRCStream.CRC;
            }

            return ps;
        }

    }
}
