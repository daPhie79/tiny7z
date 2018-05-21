using pdj.tiny7z.Common;
using pdj.tiny7z.Compression;
using System;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;

namespace pdj.tiny7z.Archive
{
    class SevenZipStreamsExtractor
    {
        Stream stream;
        SevenZipHeader.StreamsInfo streamsInfo;

        public SevenZipStreamsExtractor(Stream stream, SevenZipHeader.StreamsInfo streamsInfo)
        {
            this.stream = stream;
            this.streamsInfo = streamsInfo;
        }

        public void Extract(ulong outStreamIndex, Stream outStream)
        {
            ExtractMultiple(new ulong[] { outStreamIndex }, (ulong i) => {
                return outStream;
            });
        }

        public void ExtractAll(Func<ulong, Stream> onStreamRequest, Action<ulong, Stream> onStreamClose = null)
        {
            ExtractMultiple(new ulong[0], onStreamRequest, onStreamClose);
        }

        public void ExtractMultiple(ulong[] outStreamIndices, Func<ulong, Stream> onStreamRequest, Action<ulong, Stream> onStreamClose = null)
        {
            // sequentially scan through folders and unpacked streams
            ulong index = 0;
            ulong packIndex = 0;
            for (ulong i = 0; i < streamsInfo.UnPackInfo.NumFolders; ++i)
            {
                // only one unpack stream in folder
                if (streamsInfo.SubStreamsInfo == null || !streamsInfo.SubStreamsInfo.NumUnPackStreamsInFolders.Any())
                {
                    if (!outStreamIndices.Any() || outStreamIndices.Contains(index))
                        extractMultipleFromFolder(index, null, i, packIndex, onStreamRequest, onStreamClose);

                    ++index;
                }
                else // or multiple streams
                {
                    ulong numStreams = streamsInfo.SubStreamsInfo.NumUnPackStreamsInFolders[i];
                    ulong osiOffset = index;

                    bool[] matches = new bool[numStreams];
                    for (ulong j = 0; j < numStreams; ++j, ++index)
                        matches[j] = (!outStreamIndices.Any() || outStreamIndices.Contains(index));

                    if (matches.Any(match => match == true))
                        extractMultipleFromFolder(osiOffset, matches, i, packIndex, onStreamRequest, onStreamClose);
                }
                packIndex += streamsInfo.UnPackInfo.Folders[i].NumPackedStreams;
            }
        }

        private void extractMultipleFromFolder(ulong outputStreamIndexOffset, bool[] matches, ulong folderIndex, ulong packIndex, Func<ulong, Stream> onStreamRequest, Action<ulong, Stream> onStreamClose)
        {
            // ensure compatible coders
            SevenZipHeader.Folder folder = streamsInfo.UnPackInfo.Folders[folderIndex];
            if (folder.NumCoders > 1)
            {
                Trace.TraceWarning("7zip: Only one coder per folder is supported for now.");
                return;
            }

            // find codec
            CodecID codecID = new CodecID(folder.CodersInfo[0].CodecId);
            Codec codec = Codec.Query(codecID);
            if (codec == null)
            {
                string codecName = SevenZipMethods.List.Where(id => id.Key == codecID).Select(id => id.Value).FirstOrDefault();
                Trace.TraceWarning("7zip: Codec `" + (codecName ?? "unknown") + "` not supported.");
                return;
            }

            // find initial position of packed streams
            ulong packPos = streamsInfo.PackInfo.PackPos + (ulong)Marshal.SizeOf(typeof(SevenZipArchive.SignatureHeader));
            ulong[] packedIndices = folder.PackedIndices ?? new ulong[] { 0 };
            for (ulong i = 0; i < packIndex + packedIndices[0]; ++i)
                packPos += streamsInfo.PackInfo.Sizes[i];

            // set stream positions and sizes
            stream.Position = (long)packPos;
            ulong inSize = streamsInfo.PackInfo.Sizes[packIndex + packedIndices[0]];
            ulong outSize = folder.GetUnPackSize();

            // set decompressor
            codec.SetDecoderProperties(folder.CodersInfo[0].Properties);
            ICoder decoder = codec.GetDecompressor();

            // define output stream
            Stream outStream;
            if (matches == null)
            {
                outStream = onStreamRequest(outputStreamIndexOffset);
            }
            else
            {
                ulong numStreams = streamsInfo.SubStreamsInfo.NumUnPackStreamsInFolders[folderIndex];

                // create complex multistream
                MultiStream multi = new MultiStream(numStreams,
                    (ulong innerIndex) =>
                    {
                        Stream innerStream = null;
                        if (matches[innerIndex])
                        {
                            innerStream = onStreamRequest(outputStreamIndexOffset + innerIndex);
                            if (innerStream == null)
                                matches[innerIndex] = false;
                        }

                        return innerStream ??
                            new NullStream((long)streamsInfo.SubStreamsInfo.UnPackSizes[(int)(outputStreamIndexOffset + innerIndex)]);
                    },
                    (ulong innerIndex, Stream stream) =>
                    {
                        if (matches[innerIndex])
                            onStreamClose(outputStreamIndexOffset + innerIndex, stream);
                    });

                // set sizes in multistream
                for (ulong i = 0; i < numStreams; ++i)
                    multi.Sizes[i] = (long)streamsInfo.SubStreamsInfo.UnPackSizes[(int)(outputStreamIndexOffset + i)];

                // set new stream as output stream
                outStream = multi;
            }

            // actual extraction is done here
            decoder.Code(stream, outStream, (long)inSize, (long)outSize, null);

            // call stream close delegate if only one stream and delegate present
            if (matches == null)
            {
                onStreamClose?.Invoke(outputStreamIndexOffset, outStream);
            }

        }

    }
}
