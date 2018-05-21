﻿using pdj.tiny7z.Common;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace pdj.tiny7z.Archive
{
    public class SevenZipCompressor : ICompressor
    {
        #region Properties
        public ReadOnlyCollection<SevenZipArchiveFile> Files
        {
            get; private set;
        }
        private List<SevenZipArchiveFile> _Files;

        public bool Solid
        {
            get; set;
        }

        public bool CompressHeader
        {
            get; set;
        }

        public bool PreserveDirectoryStructure
        {
            get; set;
        }
        #endregion

        #region Private members
        Stream stream;
        SevenZipHeader header;
        #endregion

        #region Public methods
        public SevenZipCompressor(Stream stream, SevenZipHeader header)
        {
            this.stream = stream;
            this.header = header;

            if (stream == null || !stream.CanWrite)
                throw new ArgumentNullException("Stream isn't suitable for extraction.");

            if (header == null)
                throw new ArgumentNullException("Header has not been prepared properly.");

            _Files = new List<SevenZipArchiveFile>();
            Files = new ReadOnlyCollection<SevenZipArchiveFile>(_Files);
            Solid = true;
            CompressHeader = true;
        }

        public ICompressor AddDirectory(string inputDirectory, string archiveDirectory = null, bool recursive = true)
        {
            Trace.TraceInformation($"Adding files from directory `{inputDirectory}`.");
            Trace.Indent();
            try
            {
                inputDirectory = new Uri(inputDirectory).LocalPath;
                if (!Directory.Exists(inputDirectory))
                    throw new ArgumentException($"Input directory `{inputDirectory}` does not exist.");
                if (archiveDirectory == null)
                    archiveDirectory = "";
                if (archiveDirectory != string.Empty)
                    archiveDirectory = archiveDirectory.Replace('\\', '/').Trim('/') + '/';

                List<SevenZipArchiveFile> addedFiles = new List<SevenZipArchiveFile>();
                if (PreserveDirectoryStructure && recursive)
                {
                    foreach (var dir in new DirectoryInfo(inputDirectory).EnumerateDirectories("*.*", SearchOption.AllDirectories))
                    {
                        Trace.TraceInformation("Adding: " + dir.FullName);
                        addedFiles.Add(new SevenZipArchiveFile()
                        {
                            Name = archiveDirectory + dir.FullName.Substring(inputDirectory.Length),
                            Size = 0,
                            Time = dir.LastWriteTimeUtc,
                            Attributes = (UInt32)dir.Attributes,
                            IsEmpty = true,
                            IsDirectory = true,
                            IsDeleted = false,
                        });
                    }
                }

                foreach (var file in new DirectoryInfo(inputDirectory).EnumerateFiles("*.*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
                {
                    Trace.TraceInformation("Adding: " + file);
                    addedFiles.Add(new SevenZipArchiveFile()
                    {
                        Name = archiveDirectory + (PreserveDirectoryStructure ? file.FullName.Substring(inputDirectory.Length) : Path.GetFileName(file.FullName)),
                        Size = (UInt64)file.Length,
                        Time = (DateTime)file.LastWriteTimeUtc,
                        Attributes = (UInt32)file.Attributes,
                        IsEmpty = (file.Length == 0),
                        IsDirectory = false,
                        IsDeleted = false,
                        Source = new MultiFileStream.Source(file.FullName)
                    });
                }
                _Files.AddRange(addedFiles);
            }
            finally
            {
                Trace.Unindent();
                Trace.TraceInformation("Done adding files.");
            }

            return this;
        }

        public ICompressor AddFile(string inputFileName, string archiveFileName = null)
        {
            Trace.TraceInformation($"Adding file `{inputFileName}`.");
            if (!File.Exists(inputFileName))
                throw new ArgumentException($"File `{Path.GetFileName(inputFileName)}` does not exist.");
            if (archiveFileName == null)
                archiveFileName = Path.GetFileName(inputFileName);
            archiveFileName = archiveFileName.Substring(Path.GetPathRoot(archiveFileName).Length).Replace('\\', '/');

            var fileInfo = new FileInfo(inputFileName);
            _Files.Add(new SevenZipArchiveFile()
            {
                Name = archiveFileName,
                Size = (UInt64)fileInfo.Length,
                Time = (DateTime)fileInfo.LastWriteTimeUtc,
                Attributes = (UInt32)fileInfo.Attributes,
                IsEmpty = (fileInfo.Length == 0),
                IsDirectory = false,
                IsDeleted = false,
                Source = new MultiFileStream.Source(inputFileName)
            });

            return this;
        }

        public ICompressor AddFile(Stream stream, string archiveFileName, DateTime? time = null)
        {
            Trace.TraceInformation($"Adding file `{archiveFileName}` from stream.");
            archiveFileName = archiveFileName.Substring(Path.GetPathRoot(archiveFileName).Length).Replace('\\', '/');

            _Files.Add(new SevenZipArchiveFile()
            {
                Name = archiveFileName,
                Size = (UInt64)stream.Length,
                Time = time,
                Attributes = 0,
                IsEmpty = (stream.Length == 0),
                IsDirectory = false,
                IsDeleted = false,
                Source = new MultiFileStream.Source(stream)
            });

            return this;
        }

        public ICompressor Finalize()
        {
            Trace.TraceInformation($"Compressing files.");
            Trace.Indent();
            try
            {
                buildFilesInfo();

                // index files
                var streamToFileIndex = new Dictionary<ulong, ulong>();
                ulong streamIndex = 0;
                for (long i = 0; i < _Files.LongCount(); ++i)
                {
                    if (!_Files[(int)i].IsEmpty)
                    {
                        _Files[(int)i].UnPackIndex = streamIndex;
                        streamToFileIndex[streamIndex++] = (ulong)i;
                    }
                }

                // compress files
                this.header.RawHeader.MainStreamsInfo = new SevenZipHeader.StreamsInfo();
                if (Solid && streamIndex > 1)
                {
                    compressFilesSolid(streamIndex, streamToFileIndex);
                }
                else
                {
                    compressFilesNonSolid(streamIndex, streamToFileIndex);
                }

                // write headers
                writeHeaders();
            }
            finally
            {
                Trace.Unindent();
                Trace.TraceInformation("Done compressing files.");
            }

            return this;
        }
        #endregion

        #region Private methods
        void compressFilesSolid(ulong numStreams, Dictionary<ulong, ulong> streamToFileIndex)
        {
            var sc = new z7StreamsCompressor(stream);
            // sc.Codec = Compress.Codec.Query(new Compress.CodecID(0x03, 0x01, 0x01));

            Trace.TraceInformation($"Compressing `{numStreams} files` into a solid block...");

            // actual compression using a sequence file stream and stream compressor
            var inputStream = new MultiFileStream(
                FileAccess.Read,
                streamToFileIndex.Select(sfi => _Files[(int)sfi.Value].Source).ToArray());
            z7StreamsCompressor.PackedStream cs = sc.Compress(inputStream);

            // build headers
            var streamsInfo = this.header.RawHeader.MainStreamsInfo;
            streamsInfo.PackInfo = new SevenZipHeader.PackInfo()
            {
                NumPackStreams = cs.NumStreams,
                PackPos = 0,
                Sizes = cs.Sizes,
                Digests = new SevenZipHeader.Digests(cs.NumStreams)
                {
                    CRCs = cs.CRCs
                }
            };
            streamsInfo.UnPackInfo = new SevenZipHeader.UnPackInfo()
            {
                NumFolders = 1,
                Folders = new SevenZipHeader.Folder[1]
                {
                    cs.Folder
                }
            };
            streamsInfo.UnPackInfo.Folders[0].UnPackCRC = null;
            streamsInfo.SubStreamsInfo = new SevenZipHeader.SubStreamsInfo(streamsInfo.UnPackInfo)
            {
                NumUnPackStreamsInFolders = new UInt64[1]
                {
                    numStreams
                },
                NumUnPackStreamsTotal = numStreams,
                UnPackSizes = new List<UInt64>((int)numStreams),
                Digests = new SevenZipHeader.Digests(numStreams)
            };
            for (ulong i = 0; i < numStreams; ++i)
            {
                streamsInfo.SubStreamsInfo.UnPackSizes.Add((UInt64)inputStream.Sizes[i]);
                streamsInfo.SubStreamsInfo.Digests.CRCs[i] = inputStream.CRCs[i];
            }
        }

        void compressFilesNonSolid(ulong numStreams, Dictionary<ulong, ulong> streamToFileIndex)
        {
            var sc = new z7StreamsCompressor(stream);

            // actual compression (into a single packed stream per file)
            z7StreamsCompressor.PackedStream[] css = new z7StreamsCompressor.PackedStream[numStreams];
            ulong numPackStreams = 0;
            for (ulong i = 0; i < numStreams; ++i)
            {
                SevenZipArchiveFile file = _Files[(int)streamToFileIndex[i]];

                Trace.TraceInformation($"Compressing `{file.Name}`, Size: `{file.Size} bytes`...");
                using (Stream source = file.Source.Get(FileAccess.Read))
                    css[i] = sc.Compress(source);

                numPackStreams += css[i].NumStreams;
            }

            // build headers
            var streamsInfo = this.header.RawHeader.MainStreamsInfo;
            streamsInfo.PackInfo = new SevenZipHeader.PackInfo()
            {
                NumPackStreams = numPackStreams,
                PackPos = 0,
                Sizes = new UInt64[numPackStreams],
                Digests = new SevenZipHeader.Digests(numPackStreams)
            };
            streamsInfo.UnPackInfo = new SevenZipHeader.UnPackInfo()
            {
                NumFolders = numStreams,
                Folders = new SevenZipHeader.Folder[numStreams]
            };
            for (ulong i = 0, k = 0; i < numStreams; ++i)
            {
                for (ulong j = 0; j < css[i].NumStreams; ++j, ++k)
                {
                    streamsInfo.PackInfo.Sizes[k] = css[i].Sizes[j];
                    streamsInfo.PackInfo.Digests.CRCs[k] = css[i].CRCs[j];
                }
                streamsInfo.UnPackInfo.Folders[i] = css[i].Folder;
            }
        }

        void writeHeaders()
        {
            // current position is defined as end of packed streams and beginning of header
            long endOfPackedStreamsPosition = this.stream.Position;

            // write headers in temporary stream
            var headerStream = new MemoryStream();
            header.Write(headerStream);

            // go through compressing again for headers
            if (CompressHeader)
            {
                // get compressor and default codec
                var sc = new z7StreamsCompressor(stream);

                // compress
                headerStream.Position = 0;
                z7StreamsCompressor.PackedStream cs = sc.Compress(headerStream);

                // create encoded header
                SevenZipHeader.StreamsInfo headerStreamsInfo = new SevenZipHeader.StreamsInfo()
                {
                    PackInfo = new SevenZipHeader.PackInfo()
                    {
                        NumPackStreams = cs.NumStreams,
                        PackPos = (UInt64)(endOfPackedStreamsPosition - Marshal.SizeOf(typeof(SevenZipArchive.SignatureHeader))),
                        Sizes = cs.Sizes,
                        Digests = new SevenZipHeader.Digests(1)
                        {
                            CRCs = cs.CRCs
                        }
                    },
                    UnPackInfo = new SevenZipHeader.UnPackInfo()
                    {
                        Folders = new SevenZipHeader.Folder[1] { cs.Folder },
                        NumFolders = 1
                    }
                };

                // bait and switch
                header.RawHeader = null;
                header.EncodedHeader = headerStreamsInfo;

                // write new header in headerStream
                headerStream.Dispose();
                headerStream = new MemoryStream();
                header.Write(headerStream);

                // update new end of packed position for encoded header
                endOfPackedStreamsPosition = this.stream.Position;
            }

            // create start header and calculate header crc
            headerStream.Position = 0;
            var startHeader = new SevenZipArchive.StartHeader()
            {
                NextHeaderOffset = (UInt64)(endOfPackedStreamsPosition - Marshal.SizeOf(typeof(SevenZipArchive.SignatureHeader))),
                NextHeaderSize = (UInt64)headerStream.Length,
                NextHeaderCRC = CRC.Calculate(headerStream)
            };

            // write headers at the end of output stream
            headerStream.Position = 0;
            headerStream.CopyTo(stream);

            // regenerate signature header with positions and crcs
            var signatureHeader = new SevenZipArchive.SignatureHeader()
            {
                Signature = SevenZipArchive.kSignature.ToArray(),
                ArchiveVersion = new SevenZipArchive.ArchiveVersion()
                {
                    Major = 0,
                    Minor = 2,
                },
                StartHeaderCRC = CRC.Calculate(startHeader.GetByteArray()),
                StartHeader = startHeader
            };

            // write start header and flush all pending writes
            stream.Position = 0;
            stream.Write(signatureHeader.GetByteArray(), 0, Marshal.SizeOf(signatureHeader));
            stream.Flush();
        }

        void buildFilesInfo()
        {
            // scan files for empty streams and files (directories and zero-length files)

            bool[] emptyStreams = new bool[_Files.LongCount()];
            UInt64 numEmptyStreams = 0;
            bool[] emptyFiles = new bool[_Files.LongCount()];
            UInt64 numEmptyFiles = 0;
            for (long i = 0, j = 0; i < _Files.LongCount(); ++i)
            {
                var file = _Files[(int)i];
                if (file.IsEmpty)
                {
                    emptyStreams[i] = true;
                    emptyFiles[j++] = !file.IsDirectory;

                    numEmptyStreams++;
                    numEmptyFiles += (file.IsDirectory ? (ulong)0 : 1);
                }
                else
                {
                    emptyStreams[i] = false;
                }
            }
            Array.Resize(ref emptyFiles, (int)numEmptyFiles);

            // add properties to file property headers

            var propertyEmptyStream = new SevenZipHeader.PropertyEmptyStream((ulong)_Files.LongCount())
            {
                IsEmptyStream = emptyStreams,
                NumEmptyStreams = numEmptyStreams
            };
            var propertyEmptyFile = new SevenZipHeader.PropertyEmptyFile((ulong)_Files.LongCount(), numEmptyStreams)
            {
                IsEmptyFile = emptyFiles
            };
            var propertyName = new SevenZipHeader.PropertyName((ulong)_Files.LongCount())
            {
                Names = new string[_Files.LongCount()]
            };
            var propertyTime = new SevenZipHeader.PropertyTime(SevenZipHeader.PropertyID.kMTime, (ulong)_Files.LongCount())
            {
                Times = new DateTime?[_Files.LongCount()]
            };
            var propertyAttr = new SevenZipHeader.PropertyAttributes((ulong)_Files.LongCount())
            {
                Attributes = new UInt32?[_Files.LongCount()]
            };
            for (long i = 0; i < _Files.LongCount(); ++i)
            {
                propertyName.Names[i] = _Files[(int)i].Name;
                propertyTime.Times[i] = _Files[(int)i].Time;
                propertyAttr.Attributes[i] = _Files[(int)i].Attributes;
            }

            // create header and add FilesInfo elements

            var header = this.header.RawHeader;
            header.FilesInfo = new SevenZipHeader.FilesInfo()
            {
                NumFiles = (ulong)_Files.LongCount(),
                NumEmptyStreams = numEmptyStreams
            };
            if (numEmptyStreams > 0)
                header.FilesInfo.Properties.Add(propertyEmptyStream);
            if (numEmptyFiles > 0)
                header.FilesInfo.Properties.Add(propertyEmptyFile);
            header.FilesInfo.Properties.AddRange(new SevenZipHeader.FileProperty[] { propertyName, propertyTime, propertyAttr });
        }
        #endregion
    }
}