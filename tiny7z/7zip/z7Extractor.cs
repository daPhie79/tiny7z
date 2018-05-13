using pdj.tiny7z.Common;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace pdj.tiny7z
{
    /// <summary>
    /// 7zip extractor class to extract files off an archive by filenames or index.
    /// </summary>
    public class z7Extractor : Archive.IExtractor
    {
        #region Properties
        public IReadOnlyCollection<z7ArchiveFile> Files
        {
            get; private set;
        }
        private z7ArchiveFile[] _Files;

        public bool OverwriteExistingFiles
        {
            get; set;
        }

        public bool SkipExistingFiles
        {
            get; set;
        }

        public bool PreserveDirectoryStructure
        {
            get; set;
        }

        public bool AllowFileDeletions
        {
            get; set;
        }
        #endregion

        #region Private members
        Stream stream;
        z7Header header;
        #endregion

        #region Public methods
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

        public Archive.IExtractor ExtractArchive(string outputDirectory)
        {
            return ExtractFiles(new UInt64[0], outputDirectory);
        }

        public Archive.IExtractor ExtractArchive(Func<Archive.ArchiveFile, Stream> onStreamRequest, Action<Archive.ArchiveFile, Stream> onStreamClose = null)
        {
            return ExtractFiles(new UInt64[0], onStreamRequest, onStreamClose);
        }

        public Archive.IExtractor ExtractFile(string fileName, string outputDirectory)
        {
            long index = findFileIndex(fileName, true);
            if (index == -1)
                throw new FileNotFoundException($"`{fileName}` not found.");
            return ExtractFile((UInt64)index, outputDirectory);
        }

        public Archive.IExtractor ExtractFile(string fileName, Stream outputStream)
        {
            long index = findFileIndex(fileName, true);
            if (index == -1)
                throw new FileNotFoundException($"`{fileName}` not found.");
            return ExtractFile((UInt64)index, outputStream);
        }

        public Archive.IExtractor ExtractFile(UInt64 index, string outputDirectory)
        {
            if (index >= (ulong)_Files.LongLength)
                throw new ArgumentOutOfRangeException($"Index `{index}` is out of range.");

            z7ArchiveFile file = _Files[index];

            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            if (!processFile(outputDirectory, file))
            {
                string fullPath = Path.Combine(outputDirectory, PreserveDirectoryStructure ? file.Name : Path.GetFileName(file.Name));
                if (File.Exists(fullPath) && !OverwriteExistingFiles && !SkipExistingFiles)
                    throw new IOException($"File `{file.Name}` already exists.");
                if (!File.Exists(fullPath) || OverwriteExistingFiles)
                {
                    Trace.TraceInformation($"Filename: `{file.Name}`, file size: `{file.Size} bytes`.");

                    var sx = new z7StreamsExtractor(stream, header.RawHeader.MainStreamsInfo);
                    using (Stream fileStream = File.Create(fullPath))
                        sx.Extract((UInt64)file.UnPackIndex, fileStream);
                    if (file.Time != null)
                        File.SetLastWriteTimeUtc(fullPath, (DateTime)file.Time);
                }
                else
                    Trace.TraceWarning($"File `{file.Name} already exists, skipping.");
            }

            return this;
        }

        public Archive.IExtractor ExtractFile(UInt64 index, Stream outputStream)
        {
            if (index >= (ulong)_Files.LongLength)
                throw new ArgumentOutOfRangeException($"Index `{index}` is out of range.");

            if (outputStream == null || !outputStream.CanWrite)
                throw new ArgumentException($"Stream `{nameof(outputStream)}` is invalid or cannot be written to.");

            z7ArchiveFile file = _Files[index];

            if (file.IsEmpty)
            {
                Trace.TraceWarning($"Filename: {file.Name} is a directory, empty file or anti file, nothing to output to stream.");
            }
            else
            {
                Trace.TraceInformation($"Filename: `{file.Name}`, file size: `{file.Size} bytes`.");

                var sx = new z7StreamsExtractor(stream, header.RawHeader.MainStreamsInfo);
                sx.Extract((UInt64)file.UnPackIndex, outputStream);
            }

            return this;
        }

        public Archive.IExtractor ExtractFiles(string[] fileNames, string outputDirectory)
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

        public Archive.IExtractor ExtractFiles(string[] fileNames, Func<Archive.ArchiveFile, Stream> onStreamRequest, Action<Archive.ArchiveFile, Stream> onStreamClose = null)
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

        public Archive.IExtractor ExtractFiles(UInt64[] indices, string outputDirectory)
        {
            if (indices.Any(index => index >= (ulong)_Files.LongLength))
                throw new ArgumentOutOfRangeException("An index given in `indices[]` array is out of range.");

            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            var streamToFileIndex = new Dictionary<ulong, ulong>();
            var streamIndices = new List<ulong>();
            ulong streamIndex = 0;
            for (ulong i = 0; i < (ulong)_Files.LongLength; ++i)
            {
                if (!indices.Any() || Array.IndexOf(indices, i) != -1)
                {
                    if (!processFile(outputDirectory, _Files[i]))
                        if (indices.Any())
                            streamIndices.Add(streamIndex);
                }
                if (!_Files[i].IsEmpty)
                    streamToFileIndex[streamIndex++] = i;
            }

            var sx = new z7StreamsExtractor(stream, header.RawHeader.MainStreamsInfo);
            sx.ExtractMultiple(
                streamIndices.ToArray(),

                (ulong index) => {
                    z7ArchiveFile file = _Files[streamToFileIndex[index]];
                    string fullPath = Path.Combine(outputDirectory, PreserveDirectoryStructure ? file.Name : Path.GetFileName(file.Name));

                    Trace.TraceInformation($"File index {index}, filename: {file.Name}, file size: {file.Size}");

                    if (File.Exists(fullPath) && !OverwriteExistingFiles && !SkipExistingFiles)
                        throw new IOException($"File `{file.Name}` already exists.");
                    if (!File.Exists(fullPath) || OverwriteExistingFiles)
                        return File.Create(fullPath);

                    Trace.TraceWarning($"File `{file.Name} already exists, skipping.");
                    return null;
                },

                (ulong index, Stream stream) => {
                    z7ArchiveFile file = _Files[streamToFileIndex[index]];
                    string fullPath = Path.Combine(outputDirectory, file.Name);
                    if (file.Time != null)
                        File.SetLastWriteTimeUtc(fullPath, (DateTime)file.Time);
                });

            return this;
        }

        public Archive.IExtractor ExtractFiles(UInt64[] indices, Func<Archive.ArchiveFile, Stream> onStreamRequest, Action<Archive.ArchiveFile, Stream> onStreamClose = null)
        {
            if (indices.Any(index => index >= (ulong)_Files.LongLength))
                throw new ArgumentOutOfRangeException("An index given in `indices[]` array is out of range.");

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

            var sx = new z7StreamsExtractor(stream, header.RawHeader.MainStreamsInfo);
            sx.ExtractMultiple(
                indices == null ? null : streamIndices.ToArray(),
                (ulong index) => onStreamRequest(_Files[streamToFileIndex[index]]),
                (ulong index, Stream stream) => onStreamClose?.Invoke(_Files[streamToFileIndex[index]], stream));

            return this;
        }
        #endregion

        #region Private methods
        bool processFile(string outputDirectory, z7ArchiveFile file)
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
            else if (file.IsEmpty)
            {
                if (File.Exists(fullPath) && !OverwriteExistingFiles && !SkipExistingFiles)
                    throw new IOException($"File `{file.Name}` already exists.");
                if (!File.Exists(fullPath) || OverwriteExistingFiles)
                {
                    Trace.TraceInformation($"Creating empty file \"{file.Name}\"");
                    File.WriteAllBytes(fullPath, new byte[0]);
                    if (file.Time != null)
                        File.SetLastWriteTimeUtc(fullPath, (DateTime)file.Time);
                }
                else
                    Trace.TraceWarning($"File `{file.Name}` already exists. Skipping.");
            }
            else
            {
                // regular file, not "processed"
                return false;
            }

            // it's been "processed", no further processing necessary
            return true;
        }

        long findFileIndex(string Name, bool exactPath = false)
        {
            for (long i = 0; i < _Files.LongLength; ++i)
            {
                if ((exactPath && _Files[i].Name.Equals(Name)) ||
                    (!exactPath && Path.GetFileName(_Files[i].Name).Equals(Path.GetFileName(Name))))
                    return i;
            }
            return -1;
        }

        void buildFilesIndex()
        {
            // build empty index

            var filesInfo = header.RawHeader.FilesInfo;
            _Files = new z7ArchiveFile[filesInfo.NumFiles];
            for (ulong i = 0; i < filesInfo.NumFiles; ++i)
                _Files[i] = new z7ArchiveFile();
            Files = new ReadOnlyCollection<z7ArchiveFile>(_Files);

            // set properties that are contained in FileProperties structures

            foreach (var properties in filesInfo.Properties)
            {
                switch (properties.PropertyID)
                {
                    case z7Header.PropertyID.kEmptyStream:
                        for (long i = 0; i < _Files.LongLength; ++i)
                        {
                            bool val = (properties as z7Header.PropertyEmptyStream).IsEmptyStream[i];
                            _Files[i].IsEmpty = val;
                            _Files[i].IsDirectory = val;
                        }
                        break;
                    case z7Header.PropertyID.kEmptyFile:
                        for (long i = 0, j = 0 ; i < _Files.LongLength; ++i)
                            if (_Files[i].IsEmpty)
                            {
                                bool val = (properties as z7Header.PropertyEmptyFile).IsEmptyFile[j++];
                                _Files[i].IsDirectory = !val;
                            }
                        break;
                    case z7Header.PropertyID.kAnti:
                        for (long i = 0, j = 0; i < _Files.LongLength; ++i)
                            if (_Files[i].IsEmpty)
                                _Files[i].IsDeleted = (properties as z7Header.PropertyAnti).IsAnti[j++];
                        break;
                    case z7Header.PropertyID.kMTime:
                        for (long i = 0; i < _Files.LongLength; ++i)
                            _Files[i].Time = (properties as z7Header.PropertyTime).Times[i];
                        break;
                    case z7Header.PropertyID.kName:
                        for (long i = 0; i < _Files.LongLength; ++i)
                            _Files[i].Name = (properties as z7Header.PropertyName).Names[i];
                        break;
                    case z7Header.PropertyID.kWinAttributes:
                        for (long i = 0; i < _Files.LongLength; ++i)
                            _Files[i].Attributes = (properties as z7Header.PropertyAttributes).Attributes[i];
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

                    while (_Files[fileIndex].IsEmpty)
                        if (++fileIndex >= _Files.LongLength)
                            throw new z7Exception("Missing Files entries for defined sizes.");
                    _Files[fileIndex].Size = size;
                    _Files[fileIndex].UnPackIndex = (UInt64?)streamIndex++;
                }
            }

        }
        #endregion
    }
}
