using pdj.tiny7z.Common;
using pdj.tiny7z.Compression;
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

        public Codec Codec
        {
            get; set;
        }

        Stream stream;

        public z7StreamsCompressor(Stream stream)
        {
            this.stream = stream;
            Codec = null;
        }

        public PackedStream Compress(Stream inputStream)
        {
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
                            Attributes = (Byte)Codec.ID.Size,
                            CodecId = Codec.ID.Raw.ToArray(),
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

            // get and setup compressor
            ICoder encoder = Codec.GetCompressor();
            if (encoder is IWriteCoderProperties)
            {
                using (var propsStream = new MemoryStream())
                {
                    (encoder as IWriteCoderProperties).WriteCoderProperties(propsStream);

                    ps.Folder.CodersInfo[0].Attributes |= (Byte)SevenZipHeader.CoderInfo.AttrHasAttributes;
                    ps.Folder.CodersInfo[0].Properties = propsStream.ToArray();
                    ps.Folder.CodersInfo[0].PropertiesSize = (UInt64)ps.Folder.CodersInfo[0].Properties.Length;
                }
            }

            // encode while calculating CRCs
            long outStreamStartOffset = this.stream.Position;
            long inStreamStartOffset = inputStream.Position;
            using (var inCRCStream = new Common.CRCStream(inputStream))
            using (var outCRCStream = new Common.CRCStream(stream))
            {
                encoder.Code(inCRCStream, outCRCStream, -1, -1, null);
                stream.Flush();

                ps.Folder.UnPackSizes[0] = (UInt64)(inputStream.Position - inStreamStartOffset);
                ps.Folder.UnPackCRC = inCRCStream.CRC;
                ps.Sizes[0] = (UInt64)(this.stream.Position - outStreamStartOffset);
                ps.CRCs[0] = outCRCStream.CRC;
            }

            return ps;
        }

    }
}
