using pdj.tiny7z.Compress;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace pdj.tiny7z
{
    class z7StreamsCompressor
    {
        /// <summary>
        /// This allows setting main compression codec
        /// </summary>
        public Compress.Codec Codec
        {
            get; set;
        }

        /// <summary>
        /// Private variables
        /// </summary>
        Stream stream;
        z7Header.StreamsInfo streamsInfo;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public z7StreamsCompressor(Stream stream, z7Header.StreamsInfo streamsInfo)
        {
            this.stream = stream;
            this.streamsInfo = streamsInfo;
            Codec = null;
        }

        /// <summary>
        /// Contains data needed to create a header after compressing one or more streams.
        /// </summary>
        public class CompressedStream
        {
            public UInt64 NumStreams;
            public UInt64[] Sizes;
            public UInt32?[] CRCs;
            public z7Header.Folder Folder;
        }

        /// <summary>
        /// Compresses a stream (can be multiple files as well, as long as SubStreamsInfo is properly created afterwards).
        /// </summary>
        public CompressedStream Compress(Stream inputStream)
        {
            // create compressed stream information structure

            var cs = new CompressedStream()
            {
                NumStreams = 1,
                Sizes = new UInt64[1] { 0 },
                CRCs = new UInt32?[1] { null },
                Folder = new z7Header.Folder()
                {
                    NumCoders = 1,
                    CodersInfo = new z7Header.CoderInfo[1]
                    {
                        new z7Header.CoderInfo()
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

                    cs.Folder.CodersInfo[0].Attributes |= (Byte)0b00100000;
                    cs.Folder.CodersInfo[0].Properties = propsStream.ToArray();
                    cs.Folder.CodersInfo[0].PropertiesSize = (UInt64)cs.Folder.CodersInfo[0].Properties.Length;
                }
            }

            long outStreamStartOffset = this.stream.Position;
            long inStreamStartOffset = inputStream.Position;
            using (Common.CRCStream inCRCStream = new Common.CRCStream(inputStream))
            using (Common.CRCStream outCRCStream = new Common.CRCStream(stream))
            {
                encoder.Code(inCRCStream, outCRCStream, -1, -1, null);
                stream.Flush();

                cs.Folder.UnPackSizes[0] = (UInt64)(inputStream.Position - inStreamStartOffset);
                cs.Folder.UnPackCRC = inCRCStream.CRC;
                cs.Sizes[0] = (UInt64)(this.stream.Position - outStreamStartOffset);
                cs.CRCs[0] = outCRCStream.CRC;
            }

            return cs;
        }

    }
}
