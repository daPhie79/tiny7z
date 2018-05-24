using pdj.tiny7z.Common;
using System;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;

namespace pdj.tiny7z.Archive
{
    class SevenZipStreamsExtractor
    {
        #region Private members
        Stream stream;
        SevenZipHeader.StreamsInfo streamsInfo;
        #endregion

        #region Public interface
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
        #endregion

        #region Private methods
        private void extractMultipleFromFolder(ulong outputStreamIndexOffset, bool[] matches, ulong folderIndex, ulong packIndex, Func<ulong, Stream> onStreamRequest, Action<ulong, Stream> onStreamClose)
        {
            using (var decoder = createDecoderStream(folderIndex, packIndex))
            {
                // define output stream
                Stream outStream = null;
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

                // actual extraction is done here (some decoder streams require knowing output size in advance, like PPMd)
                Util.TransferTo(decoder, outStream, (long)streamsInfo.UnPackInfo.Folders[folderIndex].GetUnPackSize());

                // call stream close delegate if only one stream and delegate present
                if (matches == null)
                {
                    onStreamClose?.Invoke(outputStreamIndexOffset, outStream);
                }
            }
        }

        private Stream createDecoderStreamForCoder(Stream[] packedStreams, UInt64[] packedSizes, Stream[] outputStreams, SevenZipHeader.Folder folder, UInt64 coderIndex)
        {
            // find starting in and out id for coder
            ulong inStreamId = 0;
            ulong outStreamId = 0;
            for (ulong i = 0; i < coderIndex; ++i)
            {
                inStreamId += folder.CodersInfo[i].NumInStreams;
                outStreamId += folder.CodersInfo[i].NumOutStreams;
            }

            // create input streams
            Stream[] inStreams = new Stream[folder.CodersInfo[coderIndex].NumInStreams];
            for (int i = 0; i < inStreams.Length; ++i, ++inStreamId)
            {
                Int64 bindPairIndex = folder.FindBindPairForInStream(inStreamId);
                if (bindPairIndex == -1)
                {
                    Int64 index = folder.FindPackedIndexForInStream(inStreamId);
                    if (index == -1)
                        throw new SevenZipException("Could not find input stream binding.");
                    inStreams[i] = packedStreams[index];
                }
                else
                {
                    UInt64 pairedOutIndex = folder.BindPairsInfo[bindPairIndex].OutIndex;
                    if (outputStreams[pairedOutIndex] != default(Stream))
                        throw new SevenZipException("Overlapping stream bindings are invalid.");

                    UInt64 subCoderIndex = findCoderIndexForOutStreamIndex(folder, pairedOutIndex);
                    inStreams[i] = createDecoderStreamForCoder(packedStreams, packedSizes, outputStreams, folder, subCoderIndex);

                    if (outputStreams[pairedOutIndex] != default(Stream))
                        throw new SevenZipException("Overlapping stream bindings are invalid.");

                    outputStreams[pairedOutIndex] = inStreams[i];
                }
            }

            var methodID = new SevenZipMethods.MethodID(folder.CodersInfo[coderIndex].CodecId);
            if (!SevenZipMethods.Supported.ContainsKey(methodID))
            {
                string codecName = SevenZipMethods.List.Where(id => id.Key == methodID).Select(id => id.Value).FirstOrDefault();
                throw new SevenZipException("Compression method `" + (codecName ?? "unknown") + "` not supported.");
            }

            return Compression.Registry.GetDecoderStream(
                SevenZipMethods.Supported[methodID],
                inStreams,
                folder.CodersInfo[coderIndex].Properties,
                null,
                (long)folder.UnPackSizes[outStreamId]);
        }

        private Stream createDecoderStream(ulong folderIndex, ulong packIndex)
        {
            // find initial position of packed streams
            ulong packPos = streamsInfo.PackInfo.PackPos + (ulong)Marshal.SizeOf(typeof(SevenZipArchive.SignatureHeader));
            for (ulong i = 0; i < packIndex; ++i)
                packPos += streamsInfo.PackInfo.Sizes[i];

            // catch current folder info
            SevenZipHeader.Folder folder = streamsInfo.UnPackInfo.Folders[folderIndex];

            // create packed substreams
            Stream[] packedStreams = new Stream[folder.NumPackedStreams];
            UInt64[] packedSizes = new UInt64[folder.NumPackedStreams];
            for (ulong i = 0, currentPackPos = packPos; i < folder.NumPackedStreams; ++i)
            {
                UInt64 packedSize = streamsInfo.PackInfo.Sizes[packIndex + i];
                packedStreams[i] = new SubStream(this.stream, (long)currentPackPos, (long)packedSize);
                packedSizes[i] = packedSize;
                currentPackPos += packedSize;
            }

            // create output streams
            Stream[] outputStreams = new Stream[folder.NumOutStreamsTotal];

            // find primary output stream
            UInt64 primaryCoderIndex;
            UInt64 primaryOutStreamIndex;
            findPrimaryOutStreamIndex(folder, out primaryCoderIndex, out primaryOutStreamIndex);

            // start recursive stream creation
            return createDecoderStreamForCoder(packedStreams, packedSizes, outputStreams, folder, primaryCoderIndex);
        }

        private static UInt64 findCoderIndexForOutStreamIndex(SevenZipHeader.Folder folder, UInt64 outStreamIndex)
        {
            for (ulong coderIndex = 0, index = 0; coderIndex < folder.NumCoders; ++coderIndex)
                for (ulong i = 0; i < folder.CodersInfo[coderIndex].NumOutStreams; ++i, ++index)
                    if (outStreamIndex == index)
                        return coderIndex;
            throw new SevenZipException($"Could not find coder index for out stream index `{outStreamIndex}`.");
        }

        private static void findPrimaryOutStreamIndex(SevenZipHeader.Folder folder, out UInt64 primaryCoderIndex, out UInt64 primaryOutStreamIndex)
        {
            bool foundPrimaryOutStream = false;
            primaryCoderIndex = 0;
            primaryOutStreamIndex = 0;

            for (UInt64 outStreamIndex = 0, coderIndex = 0;
                coderIndex < (UInt64)folder.CodersInfo.LongLength;
                coderIndex++)
            {
                for (UInt64 coderOutStreamIndex = 0;
                    coderOutStreamIndex < folder.CodersInfo[coderIndex].NumOutStreams;
                    coderOutStreamIndex++, outStreamIndex++)
                {
                    if (folder.FindBindPairForOutStream(outStreamIndex) < 0)
                    {
                        if (foundPrimaryOutStream)
                            throw new SevenZipException("Multiple output streams are not supported.");

                        foundPrimaryOutStream = true;
                        primaryCoderIndex = coderIndex;
                        primaryOutStreamIndex = outStreamIndex;
                    }
                }
            }

            if (!foundPrimaryOutStream)
                throw new SevenZipException("No primary output stream in folder.");
        }
        #endregion
    }
}
