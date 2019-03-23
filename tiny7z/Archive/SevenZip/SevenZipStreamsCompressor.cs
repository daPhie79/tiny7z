using pdj.tiny7z.Common;
using ManagedLzma.LZMA;
using ManagedLzma.LZMA.Master;
using System;
using System.IO;
using System.Linq;

namespace pdj.tiny7z.Archive
{
    internal class SevenZipStreamsCompressor
    {
        #region Internal Classes
        internal class PackedStream
        {
            public UInt64 NumStreams;
            public UInt64[] Sizes;
            public UInt32?[] CRCs;
            public SevenZipHeader.Folder Folder;
        }
        #endregion Internal Classes

        #region Internal Properties
        internal Compression.Registry.Method? Method
        {
            get; set;
        }
        #endregion

        #region Internal Constructor
        internal SevenZipStreamsCompressor(Stream stream)
        {
            this.stream = stream;
            this.Method = null;
        }
        #endregion Internal Constructor

        #region Internal Methods
        internal PackedStream Compress(Stream inputStream, SevenZipProgressProvider progressProvider)
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
                LZMA.CLzmaEnc encoder = LZMA.LzmaEnc_Create(LZMA.ISzAlloc.SmallAlloc);
                LZMA.CLzmaEncProps encoderProps = LZMA.CLzmaEncProps.LzmaEncProps_Init();
                LZMA.CSeqOutStream outputHelper;
                LZMA.CSeqInStream inputHelper;
                LZMA.SRes res = LZMA.SZ_OK;

                // prepare encoder settings
                res = encoder.LzmaEnc_SetProps(encoderProps);
                if (res != LZMA.SZ_OK)
                    throw new SevenZipException("Error setting LZMA encoder properties.");
                byte[] properties = new byte[LZMA.LZMA_PROPS_SIZE];
                long binarySettingsSize = LZMA.LZMA_PROPS_SIZE;
                res = encoder.LzmaEnc_WriteProperties(properties, ref binarySettingsSize);
                if (res != LZMA.SZ_OK)
                    throw new SevenZipException("Error writing LZMA encoder properties.");
                if (binarySettingsSize != LZMA.LZMA_PROPS_SIZE)
                    throw new NotSupportedException();

                // read/write helpers
                outputHelper = new LZMA.CSeqOutStream(
                    (P<byte> buf, long sz) => {
                        outCRCStream.Write(buf.mBuffer, buf.mOffset, checked((int)sz));
                    });

                inputHelper = new LZMA.CSeqInStream(
                    (P<byte> buf, long sz) => {
                        return inCRCStream.Read(buf.mBuffer, buf.mOffset, checked((int)sz));
                    });

                // encode
                res = encoder.LzmaEnc_Encode(outputHelper, inputHelper, progressProvider, LZMA.ISzAlloc.SmallAlloc, LZMA.ISzAlloc.BigAlloc);
                if (res != LZMA.SZ_OK)
                    throw new InvalidOperationException();

                // cleanup
                encoder.LzmaEnc_Destroy(LZMA.ISzAlloc.SmallAlloc, LZMA.ISzAlloc.BigAlloc);

                // keep settings in header
                ps.Folder.CodersInfo[0].Attributes |= (Byte)SevenZipHeader.CoderInfo.AttrHasAttributes;
                ps.Folder.CodersInfo[0].Properties = properties.ToArray();
                ps.Folder.CodersInfo[0].PropertiesSize = (UInt64)ps.Folder.CodersInfo[0].Properties.Length;

                // store sizes and checksums
                ps.Folder.UnPackSizes[0] = (UInt64)(inputStream.Position - inStreamStartOffset);
                ps.Folder.UnPackCRC = inCRCStream.Result;
                ps.Sizes[0] = (UInt64)(this.stream.Position - outStreamStartOffset);
                ps.CRCs[0] = outCRCStream.Result;

                // handle progress offsets (in case compressor is called multiple times, with non-solid archives for instance)
                if (progressProvider != null)
                {
                    progressProvider.IncreaseOffsetBy((long)ps.Folder.UnPackSizes[0], (long)ps.Sizes[0]);
                    progressProvider.SetProgress(0, 0);
                }
            }

            return ps;
        }
        #endregion Internal Methods

        #region Private Field
        private Stream stream;
        #endregion Private Field
    }
}
