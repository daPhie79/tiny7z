using pdj.tiny7z.Compress;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace pdj.tiny7z
{
    class z7StreamsExtractor
    {
        /// <summary>
        /// Private variables
        /// </summary>
        Stream stream;
        z7Header.StreamsInfo streamsInfo;

        public z7StreamsExtractor(Stream stream, z7Header.StreamsInfo streamsInfo)
        {
            this.stream = stream;
            this.streamsInfo = streamsInfo;
        }

        public Stream Extract(ulong outStreamIndex)
        {
            Stream outStream = null;
            ExtractMultiple(new ulong[] { outStreamIndex }, (ulong i, Stream s) => {
                outStream = s;
            });
            return outStream;
        }

        public void ExtractAll(Action<ulong, Stream> onExtract)
        {
            ExtractMultiple(new ulong[0], onExtract);
        }

        public void ExtractMultiple(ulong[] outStreamIndices, Action<ulong, Stream> onExtract)
        {
            ulong index = 0;
            ulong packIndex = 0;
            for (ulong i = 0; i < streamsInfo.UnPackInfo.NumFolders; ++i)
            {
                var matches = new List<StreamMatch>();
                if (streamsInfo.SubStreamsInfo == null || !streamsInfo.SubStreamsInfo.NumUnPackStreamsInFolders.Any())
                {
                    if (!outStreamIndices.Any() || outStreamIndices.Contains(index))
                    {
                        matches.Add(new StreamMatch() { OutStreamIndex = index, UnPackIndexInFolder = null });
                        extractMultipleFromFolder(i, packIndex, matches, onExtract);
                    }
                    ++index;
                }
                else
                {
                    for (ulong j = 0; j < streamsInfo.SubStreamsInfo.NumUnPackStreamsInFolders[i]; ++j, ++index)
                    {
                        if (!outStreamIndices.Any() || outStreamIndices.Contains(index))
                            matches.Add(new StreamMatch() { OutStreamIndex = index, UnPackIndexInFolder = j });
                    }
                    if (matches.Any())
                        extractMultipleFromFolder(i, packIndex, matches, onExtract);
                }
                packIndex += streamsInfo.UnPackInfo.Folders[i].NumPackedStreams;
            }
        }

        /// <summary>
        /// Objects of this class are passed along when extracting matched streams.
        /// </summary>
        private class StreamMatch
        {
            public ulong OutStreamIndex;
            public ulong? UnPackIndexInFolder;
        }

        /// <summary>
        /// Internal method that decompressed files from a single extracted folder
        /// </summary>
        private void extractMultipleFromFolder(ulong folderIndex, ulong packIndex, IEnumerable<StreamMatch> matches, Action<ulong, Stream> onExtract)
        {
            z7Header.Folder folder = streamsInfo.UnPackInfo.Folders[folderIndex];
            if (folder.NumCoders > 1)
            {
                Trace.TraceWarning("7zip: Only one coder per folder is supported for now.");
                return;
            }

            Compress.CodecID codecID = new Compress.CodecID(folder.CodersInfo[0].CodecId);
            Compress.Codec codec = Compress.Codec.Query(codecID);
            if (codec == null)
            {
                string codecName = z7Methods.Codecs.Where(id => id.Key == codecID).Select(id => id.Value).FirstOrDefault();
                Trace.TraceWarning("7zip: Codec `" + (codecName ?? "unknown") + "` not supported.");
                return;
            }

            ulong packPos = streamsInfo.PackInfo.PackPos + (ulong)Marshal.SizeOf(typeof(z7Archive.SignatureHeader));
            UInt64[] packedIndices = folder.PackedIndices ?? new UInt64[] { 0 };
            for (ulong i = 0; i < packIndex + packedIndices[0]; ++i)
                packPos += streamsInfo.PackInfo.Sizes[i];

            stream.Position = (long)packPos;
            ulong inSize = streamsInfo.PackInfo.Sizes[packIndex + packedIndices[0]];
            ulong outSize = folder.GetUnPackSize();

            codec.SetDecoderProperties(folder.CodersInfo[0].Properties);
            ICoder decoder = codec.GetDecompressor();

            MemoryStream outStream = new MemoryStream((int)outSize);
            decoder.Code(stream, outStream, (long)inSize, (long)outSize, null);
            outStream.Position = 0;

            foreach (var match in matches)
            {
                if (match.UnPackIndexInFolder == null)
                    onExtract(match.OutStreamIndex, outStream);
                else
                {
                    ulong unPackPos = 0;
                    for (ulong i = match.OutStreamIndex - (ulong)match.UnPackIndexInFolder; i < match.OutStreamIndex; ++i)
                        unPackPos += streamsInfo.SubStreamsInfo.UnPackSizes[(int)i];

                    ulong unPackSize = streamsInfo.SubStreamsInfo.UnPackSizes[(int)match.OutStreamIndex];
                    onExtract(
                        match.OutStreamIndex,
                        new Common.SubStream(outStream, (long)unPackPos, (long)unPackSize));
                }
            }
        }

    }
}
