using pdj.tiny7z.Common;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace pdj.tiny7z.Archive
{
    class SevenZipStreamsCompressor
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

        public SevenZipStreamsCompressor(Stream stream)
        {
            this.stream = stream;
            this.Method = null;
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
                var encoder = new SevenZip.Compression.LZMA.Encoder();
                var encoderProperties = new SevenZip.Compression.LZMA.EncoderProperties();
                encoder.SetCoderProperties(encoderProperties.propIDs, encoderProperties.properties);
                using (var propsStream = new MemoryStream())
                {
                    encoder.WriteCoderProperties(propsStream);

                    ps.Folder.CodersInfo[0].Attributes |= (Byte)SevenZipHeader.CoderInfo.AttrHasAttributes;
                    ps.Folder.CodersInfo[0].Properties = propsStream.ToArray();
                    ps.Folder.CodersInfo[0].PropertiesSize = (UInt64)ps.Folder.CodersInfo[0].Properties.Length;
                }

                encoder.Code(inCRCStream, outCRCStream, -1, -1, null);
                stream.Flush();

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
