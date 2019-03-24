using pdj.tiny7z.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace pdj.tiny7z.Archive
{
    /// <summary>
    /// 7zip extractor class to extract files off an archive by filenames or index.
    /// </summary>
    public class SevenZipExtractor : IExtractor
    {
        #region Public Properties
        public IReadOnlyList<ArchiveFile> Files
        {
            get; private set;
        }

        public FeedbackNeededDelegate OverwriteDelegate
        {
            get; set;
        }

        public ProgressDelegate ProgressDelegate
        {
            get; set;
        }

        public bool AllowFileDeletions
        {
            get; set;
        }

        public bool OverwriteExistingFiles
        {
            get; set;
        }

        public string Password
        {
            get; set;
        }

        public bool PreserveDirectoryStructure
        {
            get; set;
        }

        public bool SkipExistingFiles
        {
            get; set;
        }
        #endregion Public Properties

        #region Public Methods
        public void Dispose() // IDisposable
        {
            Finalize();
        }

        public void Dump()
        {
            // TODO
        }

        public IExtractor ExtractArchive(string outputDirectory)
        {
            return ExtractFiles(new UInt64[0], outputDirectory);
        }

        public IExtractor ExtractArchive(Func<ArchiveFile, Stream> onStreamRequest, Action<ArchiveFile, Stream> onStreamClose = null)
        {
            return ExtractFiles(new UInt64[0], onStreamRequest, onStreamClose);
        }

        public IExtractor ExtractFile(string fileName, string outputDirectory)
        {
            long index = findFileIndex(fileName, true);
            if (index == -1)
                throw new FileNotFoundException($"`{fileName}` not found.");
            return ExtractFile((UInt64)index, outputDirectory);
        }

        public IExtractor ExtractFile(string fileName, Stream outputStream)
        {
            long index = findFileIndex(fileName, true);
            if (index == -1)
                throw new FileNotFoundException($"`{fileName}` not found.");
            return ExtractFile((UInt64)index, outputStream);
        }

        public IExtractor ExtractFile(UInt64 index, string outputDirectory)
        {
            if (index >= (ulong)_Files.LongLength)
                throw new ArgumentOutOfRangeException($"Index `{index}` is out of range.");

            SevenZipArchiveFile file = _Files[index];
            if (!preProcessFile(outputDirectory, file))
            {
                string fullPath = Path.Combine(outputDirectory, PreserveDirectoryStructure ? file.Name : Path.GetFileName(file.Name));

                // progress provider
                SevenZipProgressProvider szpp = null;
                if (ProgressDelegate != null)
                    szpp = new SevenZipProgressProvider(_Files, new[] { index }, ProgressDelegate);

                // extraction
                Trace.TraceInformation($"Filename: `{file.Name}`, file size: `{file.Size} bytes`.");
                var sx = new SevenZipStreamsExtractor(stream, header.RawHeader.MainStreamsInfo, Password);
                using (Stream fileStream = File.Create(fullPath))
                    sx.Extract((UInt64)file.UnPackIndex, fileStream, szpp);
                if (file.Time != null)
                    File.SetLastWriteTimeUtc(fullPath, (DateTime)file.Time);
            }

            return this;
        }

        public IExtractor ExtractFile(UInt64 index, Stream outputStream)
        {
            if (index >= (ulong)_Files.LongLength)
                throw new ArgumentOutOfRangeException($"Index `{index}` is out of range.");
            if (outputStream == null || !outputStream.CanWrite)
                throw new ArgumentException($"Stream `{nameof(outputStream)}` is invalid or cannot be written to.");

            SevenZipArchiveFile file = _Files[index];
            if (file.IsEmpty)
            {
                Trace.TraceWarning($"Filename: {file.Name} is a directory, empty file or anti file, nothing to output to stream.");
            }
            else
            {
                // progress provider
                SevenZipProgressProvider szpp = null;
                if (ProgressDelegate != null)
                    szpp = new SevenZipProgressProvider(_Files, new[] { index }, ProgressDelegate);

                // extraction
                Trace.TraceInformation($"Filename: `{file.Name}`, file size: `{file.Size} bytes`.");
                Trace.TraceInformation("Extracting...");
                var sx = new SevenZipStreamsExtractor(stream, header.RawHeader.MainStreamsInfo, Password);
                sx.Extract((UInt64)file.UnPackIndex, outputStream, szpp);
            }

            return this;
        }

        public IExtractor ExtractFiles(string[] fileNames, string outputDirectory)
        {
            var indices = new List<UInt64>();
            foreach (var fileName in fileNames)
            {
                long index = findFileIndex(fileName, true);
                if (index == -1)
                    throw new ArgumentOutOfRangeException($"Filename `{fileName}` doesn't exist in archive.");
                indices.Add((UInt64)index);
            }
            if (indices.Any())
                return ExtractFiles(indices.ToArray(), outputDirectory);

            return this;
        }

        public IExtractor ExtractFiles(string[] fileNames, Func<ArchiveFile, Stream> onStreamRequest, Action<ArchiveFile, Stream> onStreamClose = null)
        {
            var indices = new List<UInt64>();
            foreach (var fileName in fileNames)
            {
                long index = findFileIndex(fileName, true);
                if (index == -1)
                    throw new ArgumentOutOfRangeException($"Filename `{fileName}` doesn't exist in archive.");
                indices.Add((UInt64)index);
            }
            if (indices.Any())
                return ExtractFiles(indices.ToArray(), onStreamRequest, onStreamClose);

            return this;
        }

        public IExtractor ExtractFiles(UInt64[] indices, string outputDirectory)
        {
            if (indices.Any(index => index >= (ulong)_Files.LongLength))
                throw new ArgumentOutOfRangeException("An index given in `indices[]` array is out of range.");

            // preprocess files and keep track of streams to decompress
            var streamToFileIndex = new Dictionary<ulong, ulong>();
            var streamIndices = new List<ulong>();
            ulong streamIndex = 0;
            for (ulong i = 0; i < (ulong)_Files.LongLength; ++i)
            {
                if (!indices.Any() || Array.IndexOf(indices, i) != -1)
                {
                    if (!preProcessFile(outputDirectory, _Files[i]))
                        streamIndices.Add(streamIndex);
                }
                if (!_Files[i].IsEmpty)
                    streamToFileIndex[streamIndex++] = i;
            }

            // no file to decompress
            if (!streamIndices.Any())
            {
                Trace.TraceWarning("ExtractFiles: No decoding required.");
                return this;
            }

            // progress provider
            SevenZipProgressProvider szpp = null;
            if (ProgressDelegate != null)
                szpp = new SevenZipProgressProvider(_Files, indices, ProgressDelegate);

            // extraction
            Trace.TraceInformation("Extracting...");
            var sx = new SevenZipStreamsExtractor(stream, header.RawHeader.MainStreamsInfo, Password);
            sx.ExtractMultiple(
                streamIndices.ToArray(),
                (ulong index) => {
                    SevenZipArchiveFile file = _Files[streamToFileIndex[index]];
                    string fullPath = Path.Combine(outputDirectory, PreserveDirectoryStructure ? file.Name : Path.GetFileName(file.Name));

                    Trace.TraceInformation($"File index {index}, filename: {file.Name}, file size: {file.Size}");
                    return new FileStream(fullPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize);
                },
                (ulong index, Stream stream) => {
                    stream.Close();
                    SevenZipArchiveFile file = _Files[streamToFileIndex[index]];
                    string fullPath = Path.Combine(outputDirectory, PreserveDirectoryStructure ? file.Name : Path.GetFileName(file.Name));
                    if (file.Time != null)
                        File.SetLastWriteTimeUtc(fullPath, (DateTime)file.Time);
                },
                szpp);

            return this;
        }

        public IExtractor ExtractFiles(UInt64[] indices, Func<ArchiveFile, Stream> onStreamRequest, Action<ArchiveFile, Stream> onStreamClose = null)
        {
            if (indices.Any(index => index >= (ulong)_Files.LongLength))
                throw new ArgumentOutOfRangeException("An index given in `indices[]` array is out of range.");

            // preprocess files and keep track of streams to decompress
            var streamToFileIndex = new Dictionary<ulong, ulong>();
            var streamIndices = new List<ulong>();
            ulong streamIndex = 0;
            for (ulong i = 0; i < (ulong)_Files.LongLength; ++i)
            {
                if (!indices.Any() || Array.IndexOf(indices, i) != -1)
                {
                    if (_Files[i].IsEmpty)
                    {
                        using (Stream s = onStreamRequest(_Files[i]))
                            if (s != null)
                                onStreamClose?.Invoke(_Files[i], s);
                    }
                    else if (indices.Any())
                        streamIndices.Add(streamIndex);
                }
                if (!_Files[i].IsEmpty)
                    streamToFileIndex[streamIndex++] = i;
            }

            // no file to decompress
            if (!streamToFileIndex.Any())
            {
                Trace.TraceWarning("ExtractFiles: No decoding required.");
                return this;
            }

            // progress provider
            SevenZipProgressProvider szpp = null;
            if (ProgressDelegate != null)
                szpp = new SevenZipProgressProvider(_Files, indices, ProgressDelegate);

            // extraction
            Trace.TraceInformation("Extracting...");
            var sx = new SevenZipStreamsExtractor(stream, header.RawHeader.MainStreamsInfo, Password);
            sx.ExtractMultiple(
                streamIndices.ToArray(),
                (ulong index) => onStreamRequest(_Files[streamToFileIndex[index]]),
                (ulong index, Stream stream) => onStreamClose?.Invoke(_Files[streamToFileIndex[index]], stream),
                szpp);

            return this;
        }

        public IExtractor Finalize()
        {
            this.stream = null;
            this.header = null;
            return this;
        }
        #endregion Public Methods

        #region Internal Constructors
        internal SevenZipExtractor(Stream stream, SevenZipHeader header)
        {
            this.stream = stream;
            this.header = header;

            if (stream == null || !stream.CanRead || stream.Length == 0)
                throw new ArgumentNullException("Stream isn't suitable for extraction.");

            if (header == null || header.RawHeader == null)
                throw new ArgumentNullException("Header has not been parsed and/or decompressed properly.");

            // init file lists
            buildFilesIndex();

            // default values
            OverwriteDelegate = null;
            ProgressDelegate = null;
            AllowFileDeletions = false;
            OverwriteExistingFiles = false;
            Password = null;
            PreserveDirectoryStructure = true;
            SkipExistingFiles = false;
        }
        #endregion Internal Constructors

        #region Private Fields
        private const int bufferSize = 128 * 1024;
        private Stream stream;
        private SevenZipHeader header;
        private SevenZipArchiveFile[] _Files;
        #endregion Private Fields

        #region Private Methods
        private bool preProcessFile(string outputDirectory, SevenZipArchiveFile file)
        {
            string fullPath = Path.Combine(outputDirectory, PreserveDirectoryStructure ? file.Name : Path.GetFileName(file.Name));
            if (file.IsDeleted)
            {
                if (AllowFileDeletions && File.Exists(fullPath))
                {
                    Trace.TraceInformation($"Deleting file \"{file.Name}\"");
                    File.Delete(fullPath);
                }
            }
            else if (file.IsDirectory)
            {
                if (!PreserveDirectoryStructure)
                {
                    Trace.TraceWarning($"Directory `{file.Name}` ignored, PreserveDirectoryStructure is disabled.");
                }
                else if (!Directory.Exists(fullPath))
                {
                    Trace.TraceInformation($"Create directory \"{file.Name}\"");
                    Directory.CreateDirectory(fullPath);
                    if (file.Time != null)
                        Directory.SetLastWriteTimeUtc(fullPath, (DateTime)file.Time);
                }
            }
            else
            {
                FeedbackResult result = FeedbackResult.Yes;
                if (File.Exists(fullPath))
                {
                    if (OverwriteExistingFiles)
                    {
                        if (OverwriteDelegate != null)
                        {
                            result = OverwriteDelegate(new[] { file });
                            if (result == FeedbackResult.Cancel)
                                throw new OperationCanceledException("User feedback cancelled extraction.");
                        }
                    }
                    else
                    {
                        if (!SkipExistingFiles)
                            throw new SevenZipFileAlreadyExistsException(file);
                        result = FeedbackResult.No;
                    }
                }
                if (result == FeedbackResult.Yes)
                {
                    // make sure path exists
                    if (!string.IsNullOrEmpty(Path.GetDirectoryName(file.Name)))
                    {
                        if (!Directory.Exists(Path.GetDirectoryName(fullPath)))
                            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                    }

                    // if file is not empty, it will need extraction
                    if (!file.IsEmpty)
                    {
                        Trace.TraceInformation($"File included for extraction: {file.Name}, file size: {file.Size}");
                        return false;
                    }

                    // create empty file right away
                    Trace.TraceInformation($"Creating empty file \"{file.Name}\"");
                    File.WriteAllBytes(fullPath, new byte[0]);
                    if (file.Time != null)
                        File.SetLastWriteTimeUtc(fullPath, (DateTime)file.Time);
                }
                else
                    // skipping file, so leave it as "processed" to avoid useless decoding
                    Trace.TraceWarning($"File `{file.Name}` already exists. Skipping.");
            }

            // it's been "processed", no further processing necessary
            return true;
        }

        private long findFileIndex(string Name, bool exactPath)
        {
            for (long i = 0; i < _Files.LongLength; i++)
                if ((exactPath && (_Files[i].Name == Name)) || (!exactPath && (Path.GetFileName(Name) == Path.GetFileName(_Files[i].Name))))
                    return i;
            return -1;
        }

        private void buildFilesIndex()
        {
            // build empty index

            var filesInfo = header.RawHeader.FilesInfo;
            _Files = new SevenZipArchiveFile[filesInfo.NumFiles];
            for (ulong i = 0; i < filesInfo.NumFiles; ++i)
                _Files[i] = new SevenZipArchiveFile();
            Files = _Files;

            // set properties that are contained in FileProperties structures

            foreach (var properties in filesInfo.Properties)
            {
                switch (properties.PropertyID)
                {
                    case SevenZipHeader.PropertyID.kEmptyStream:
                        for (long i = 0; i < _Files.LongLength; ++i)
                        {
                            bool val = (properties as SevenZipHeader.PropertyEmptyStream).IsEmptyStream[i];
                            _Files[i].IsEmpty = val;
                            _Files[i].IsDirectory = val;
                        }
                        break;
                    case SevenZipHeader.PropertyID.kEmptyFile:
                        for (long i = 0, j = 0 ; i < _Files.LongLength; ++i)
                            if (_Files[i].IsEmpty)
                            {
                                bool val = (properties as SevenZipHeader.PropertyEmptyFile).IsEmptyFile[j++];
                                _Files[i].IsDirectory = !val;
                            }
                        break;
                    case SevenZipHeader.PropertyID.kAnti:
                        for (long i = 0, j = 0; i < _Files.LongLength; ++i)
                            if (_Files[i].IsEmpty)
                                _Files[i].IsDeleted = (properties as SevenZipHeader.PropertyAnti).IsAnti[j++];
                        break;
                    case SevenZipHeader.PropertyID.kMTime:
                        for (long i = 0; i < _Files.LongLength; ++i)
                            _Files[i].Time = (properties as SevenZipHeader.PropertyTime).Times[i];
                        break;
                    case SevenZipHeader.PropertyID.kName:
                        for (long i = 0; i < _Files.LongLength; ++i)
                            _Files[i].Name = (properties as SevenZipHeader.PropertyName).Names[i];
                        break;
                    case SevenZipHeader.PropertyID.kWinAttributes:
                        for (long i = 0; i < _Files.LongLength; ++i)
                            _Files[i].Attributes = (properties as SevenZipHeader.PropertyAttributes).Attributes[i];
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

            int upsIndex = 0;
            int upcIndex = 0;

            long fileIndex = 0;
            long streamIndex = 0;
            for (long i = 0; i < (long)streamsInfo.UnPackInfo.NumFolders; ++i)
            {
                SevenZipHeader.Folder folder = ui.Folders[i];
                long ups = 1;
                if (ssi != null && ssi.NumUnPackStreamsInFolders.Any())
                    ups = (long)ssi.NumUnPackStreamsInFolders[i];
                if (ups == 0)
                    throw new SevenZipException("Unexpected, no UnPackStream in Folder.");

                UInt64 size = folder.GetUnPackSize();
                UInt32? crc = folder.UnPackCRC;
                for (long j = 0; j < ups; ++j)
                {
                    if (ssi != null && ssi.UnPackSizes.Any())
                    {
                        if (upsIndex > ssi.UnPackSizes.Count())
                            throw new SevenZipException("Unexpected, missing UnPackSize entry(ies).");
                        size = ssi.UnPackSizes[upsIndex++];
                    }
                    else
                    {
                        if (ups > 1)
                            throw new SevenZipException("Missing SubStreamsInfo header chunk.");
                    }

                    if (crc == null || ups > 1)
                    {
                        if (ssi != null && ssi.Digests != null && (int)ssi.Digests.NumStreams() > upcIndex)
                        {
                            crc = ssi.Digests.CRCs[upcIndex];
                        }
                        upcIndex++;
                    }

                    while (_Files[fileIndex].IsEmpty)
                        if (++fileIndex >= _Files.LongLength)
                            throw new SevenZipException("Missing Files entries for defined sizes.");
                    _Files[fileIndex].Size = size;
                    _Files[fileIndex].CRC = crc;
                    _Files[fileIndex].UnPackIndex = (UInt64?)streamIndex;

                    fileIndex++;
                    streamIndex++;
                }
            }

        }
        #endregion Private Methods
    }
}
