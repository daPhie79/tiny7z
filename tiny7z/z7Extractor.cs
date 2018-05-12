using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pdj.tiny7z
{
    /// <summary>
    /// 7zip extractor class to extract files off an archive by name or Files.
    /// </summary>
    public class z7Extractor : Archive.IExtractor
    {
        /// <summary>
        /// Files property
        /// </summary>
        public z7ArchiveFile[] Files
        {
            get; private set;
        }

        /// <summary>
        /// Private variables
        /// </summary>
        Stream stream;
        z7Header header;

        /// <summary>
        /// Constructor
        /// </summary>
        public z7Extractor(Stream stream, z7Header header)
        {
            this.stream = stream;
            this.header = header;

            if (stream == null || !stream.CanRead || stream.Length == 0)
                throw new ArgumentNullException("Stream isn't suitable for extraction.");

            if (header == null || header.RawHeader == null)
                throw new ArgumentNullException("Header has not been parsed and/or decompressed properly.");

            buildFilesIndex();
        }

        /// <summary>
        /// Returns index of given filename
        /// </summary>
        public long FindFileIndex(string Name, bool exactPath = false)
        {
            for (long i = 0; i < Files.LongLength; ++i)
            {
                if ((exactPath && Files[i].Name.Equals(Name)) ||
                    (!exactPath && Path.GetFileName(Files[i].Name).Equals(Path.GetFileName(Name))))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Extract all files to the designated OutputPath
        /// </summary>
        public Archive.IExtractor ExtractAll(string outputPath, bool overwriteExistingFiles = false)
        {
            var streamToFileIndex = new Dictionary<ulong, ulong>();
            ulong streamIndex = 0;
            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            for (ulong i = 0; i < (ulong)Files.LongLength; ++i)
            {
                z7ArchiveFile file = Files[i];
                string fullPath = Path.Combine(outputPath, file.Name);
                if (file.IsDirectory)
                {
                    if (!Directory.Exists(fullPath))
                    {
                        Trace.TraceInformation($"Create directory \"{file.Name}\"");
                        Directory.CreateDirectory(fullPath);
                        if (file.Time != null)
                            Directory.SetLastWriteTimeUtc(fullPath, (DateTime)file.Time);
                    }
                }
                else if (file.IsEmpty)
                {
                    if (!File.Exists(fullPath) || overwriteExistingFiles)
                    {
                        Trace.TraceInformation($"Creating empty file \"{file.Name}\"");
                        File.WriteAllBytes(fullPath, new byte[0]);
                        if (file.Time != null)
                            File.SetLastWriteTimeUtc(fullPath, (DateTime)file.Time);
                    }
                }
                else if (file.IsDeleted)
                {
                    // no-op
                }
                else
                {
                    streamToFileIndex[streamIndex++] = i;
                }
            }

            var sx = new z7StreamsExtractor(stream, header.RawHeader.MainStreamsInfo);
            sx.ExtractMultiple(
                new UInt64[0],
                (ulong index, Stream fileStream) => {
                    z7ArchiveFile file = Files[streamToFileIndex[index]];
                    string fullPath = Path.Combine(outputPath, file.Name);

                    Trace.TraceInformation($"File index {index}, filename: {file.Name}, file size: {file.Size}, stream size: {fileStream.Length}");
                    if (!File.Exists(fullPath) || overwriteExistingFiles)
                    {
                        using (var outputStream = File.Create(fullPath))
                            fileStream.CopyTo(outputStream);
                        if (file.Time != null)
                            File.SetLastWriteTimeUtc(fullPath, (DateTime)file.Time);
                    }
                });

            return this;
        }

        /// <summary>
        /// Needs to be called to build a list of files and their properties off of the confusing default 7zip file Files structure
        /// </summary>
        void buildFilesIndex()
        {
            // build empty index

            var filesInfo = header.RawHeader.FilesInfo;
            Files = new z7ArchiveFile[filesInfo.NumFiles];
            for (ulong i = 0; i < filesInfo.NumFiles; ++i)
                Files[i] = new z7ArchiveFile();

            // set properties that are contained in FileProperties structures

            foreach (var properties in filesInfo.Properties)
            {
                switch (properties.PropertyID)
                {
                    case z7Header.PropertyID.kEmptyStream:
                        for (long i = 0; i < Files.LongLength; ++i)
                        {
                            bool val = (properties as z7Header.PropertyEmptyStream).IsEmptyStream[i];
                            Files[i].IsEmpty = val;
                            Files[i].IsDirectory = val;
                        }
                        break;
                    case z7Header.PropertyID.kEmptyFile:
                        for (long i = 0, j = 0 ; i < Files.LongLength; ++i)
                            if (Files[i].IsEmpty)
                            {
                                bool val = (properties as z7Header.PropertyEmptyFile).IsEmptyFile[j++];
                                Files[i].IsDirectory = !val;
                            }
                        break;
                    case z7Header.PropertyID.kAnti:
                        for (long i = 0, j = 0; i < Files.LongLength; ++i)
                            if (Files[i].IsEmpty)
                                Files[i].IsDeleted = (properties as z7Header.PropertyAnti).IsAnti[j++];
                        break;
                    case z7Header.PropertyID.kMTime:
                        for (long i = 0; i < Files.LongLength; ++i)
                            Files[i].Time = (properties as z7Header.PropertyTime).Times[i];
                        break;
                    case z7Header.PropertyID.kName:
                        for (long i = 0; i < Files.LongLength; ++i)
                            Files[i].Name = (properties as z7Header.PropertyName).Names[i];
                        break;
                    case z7Header.PropertyID.kWinAttributes:
                        for (long i = 0; i < Files.LongLength; ++i)
                            Files[i].Attributes = (properties as z7Header.PropertyAttributes).Attributes[i];
                        break;
                }
            }

            // set output sizes from the overly complex 7zip headers

            var streamsInfo = header.RawHeader.MainStreamsInfo;
            var ui = streamsInfo.UnPackInfo;
            var ssi = streamsInfo.SubStreamsInfo;
            if (ui == null)
            {
                Trace.TraceWarning("7zip: Missing header information to calculate output file sizes.");
                return;
            }

            List<UInt64>.Enumerator u = new List<UInt64>(0).GetEnumerator();
            if (ssi != null && ssi.UnPackSizes.Any())
                u = ssi.UnPackSizes.GetEnumerator();

            long fileIndex = 0;
            long streamIndex = 0;
            for (long i = 0; i < (long)streamsInfo.UnPackInfo.NumFolders; ++i)
            {
                z7Header.Folder folder = streamsInfo.UnPackInfo.Folders[i];
                long ups = 1;
                if (ssi != null && ssi.NumUnPackStreamsInFolders.Any())
                    ups = (long)ssi.NumUnPackStreamsInFolders[i];
                if (ups == 0)
                    throw new z7Exception("Unexpected, no UnPackStream in Folder.");

                UInt64 size = folder.GetUnPackSize();
                UInt32? crc = folder.UnPackCRC;
                for (long j = 0; j < ups; ++j)
                {
                    if (u.MoveNext())
                        size = u.Current;
                    else if (j > 0)
                        throw new z7Exception("Unexpected, missing UnPackSize entry(ies).");

                    while (Files[fileIndex].IsEmpty)
                        if (++fileIndex >= Files.LongLength)
                            throw new z7Exception("Missing Files entries for defined sizes.");
                    Files[fileIndex].Size = size;
                    Files[fileIndex].UnPackIndex = (UInt64?)streamIndex++;
                }
            }

        }
    }
}
