using pdj.tiny7z.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace pdj.tiny7z
{
    public class z7Compressor : Archive.ICompressor
    {
        /// <summary>
        /// Files property.
        /// </summary>
        public z7ArchiveFile[] Files
        {
            get; private set;
        }

        /// <summary>
        /// Setting this to true will compress all files into a solid block.
        /// </summary>
        public bool Solid
        {
            get; set;
        }

        /// <summary>
        /// Setting this to true will compress headers.
        /// </summary>
        public bool CompressHeader
        {
            get; set;
        }

        /// <summary>
        /// Private variables
        /// </summary>
        Stream stream;
        z7Header header;

        /// <summary>
        /// Constructor
        /// </summary>
        public z7Compressor(Stream stream, z7Header header)
        {
            this.stream = stream;
            this.header = header;

            if (stream == null || !stream.CanWrite)
                throw new ArgumentNullException("Stream isn't suitable for extraction.");

            if (header == null)
                throw new ArgumentNullException("Header has not been prepared properly.");

            Files = new z7ArchiveFile[0];
            Solid = true;
            CompressHeader = true;
        }

        /// <summary>
        /// Adds files then immediately compresses them.
        /// </summary>
        public Archive.ICompressor CompressAll(string inputPath, bool recursive = true)
        {
            AddAll(inputPath, recursive);
            compressFiles();
            writeHeaders();
            return this;
        }

        /// <summary>
        /// Adds all files from a directory (recursive or not)
        /// </summary>
        Archive.ICompressor AddAll(string inputPath, bool recursive)
        {
            Trace.TraceInformation($"Adding all files from `{inputPath}`.");
            Trace.Indent();
            try
            {
                inputPath = inputPath.Replace('\\', '/').TrimEnd('/') + '/';
                if (!Directory.Exists(inputPath))
                    throw new ArgumentException($"Input path `{inputPath}` does not exist.");

                List<z7ArchiveFile> addedFiles = new List<z7ArchiveFile>();
                if (Files.LongLength > 0)
                {
                    addedFiles.AddRange(Files);
                }
                if (recursive)
                {
                    var directories = Directory.EnumerateDirectories(inputPath, "*.*", SearchOption.AllDirectories);
                    foreach (var dir in directories)
                    {
                        Trace.TraceInformation("Adding: " + dir);
                        addedFiles.Add(new z7ArchiveFile()
                        {
                            Name = dir.Substring(inputPath.Length).Replace('\\', '/').TrimStart('/'),
                            Size = 0,
                            Time = Directory.GetLastWriteTime(dir),
                            Attributes = 0,
                            IsEmpty = true,
                            IsDirectory = true,
                            IsDeleted = false,
                        });
                    }
                }
                var files = Directory.EnumerateFiles(inputPath, "*.*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    Trace.TraceInformation("Adding: " + file);
                    var fileInfo = new FileInfo(file);
                    addedFiles.Add(new z7ArchiveFile()
                    {
                        Name = file.Substring(inputPath.Length).Replace('\\', '/').TrimStart('/'),
                        Size = (ulong)fileInfo.Length,
                        Time = File.GetLastWriteTime(file),
                        Attributes = (UInt32)File.GetAttributes(file),
                        IsEmpty = fileInfo.Length == 0,
                        IsDirectory = false,
                        IsDeleted = false,
                        InputPath = inputPath
                    });
                }
                Files = addedFiles.ToArray();
            }
            finally
            {
                Trace.Unindent();
                Trace.TraceInformation("Done adding files.");
            }

            return this;
        }

        /// <summary>
        /// Actually commits added files and compresses them into a 7zip archive.
        /// </summary>
        void compressFiles()
        {
            Trace.TraceInformation($"Compressing files.");
            Trace.Indent();
            try
            {
                buildFilesInfo();

                // index files
                var streamToFileIndex = new Dictionary<ulong, ulong>();
                ulong streamIndex = 0;
                for (long i = 0; i < Files.LongLength; ++i)
                {
                    if (!Files[i].IsEmpty)
                    {
                        Files[i].UnPackIndex = (long)streamIndex;
                        streamToFileIndex[streamIndex++] = (ulong)i;
                    }
                }

                // create main headers
                var streamsInfo = new z7Header.StreamsInfo();

                var sc = new z7StreamsCompressor(stream);
                sc.Codec = Compress.Codec.Query(new Compress.CodecID(0x03, 0x01, 0x01));
                if (Solid && streamIndex > 1)
                {
                    Trace.TraceInformation($"Compressing `{streamIndex} files` into a solid block...");

                    var inputStream = new Common.MultiFileStream(streamToFileIndex.Select(sfi => Files[sfi.Value].InputPath + Files[sfi.Value].Name).ToArray());
                    z7StreamsCompressor.CompressedStream cs = sc.Compress(inputStream);

                    streamsInfo.PackInfo = new z7Header.PackInfo()
                    {
                        NumPackStreams = cs.NumStreams,
                        PackPos = 0,
                        Sizes = cs.Sizes,
                        Digests = new z7Header.Digests(cs.NumStreams)
                        {
                            CRCs = cs.CRCs
                        }
                    };
                    streamsInfo.UnPackInfo = new z7Header.UnPackInfo()
                    {
                        NumFolders = 1,
                        Folders = new z7Header.Folder[1]
                        {
                            cs.Folder
                        }
                    };
                    streamsInfo.UnPackInfo.Folders[0].UnPackCRC = null;
                    streamsInfo.SubStreamsInfo = new z7Header.SubStreamsInfo(streamsInfo.UnPackInfo)
                    {
                        NumUnPackStreamsInFolders = new UInt64[1]
                        {
                            streamIndex
                        },
                        NumUnPackStreamsTotal = streamIndex,
                        UnPackSizes = new List<UInt64>((int)streamIndex),
                        Digests = new z7Header.Digests(streamIndex)
                    };

                    for (ulong i = 0; i < streamIndex; ++i)
                    {
                        streamsInfo.SubStreamsInfo.UnPackSizes.Add(inputStream.Sizes[i]);
                        streamsInfo.SubStreamsInfo.Digests.CRCs[i] = inputStream.CRCs[i];
                    }
                }
                else
                {
                    z7StreamsCompressor.CompressedStream[] css = new z7StreamsCompressor.CompressedStream[streamIndex];
                    ulong numPackStreams = 0;
                    for (ulong i = 0; i < streamIndex; ++i)
                    {
                        z7ArchiveFile file = Files[streamToFileIndex[i]];
                        Trace.TraceInformation($"Compressing `{file.Name}`, Size: `{file.Size} bytes`...");
                        css[i] = sc.Compress(File.OpenRead(file.InputPath + file.Name));

                        numPackStreams += css[i].NumStreams;
                    }

                    streamsInfo.PackInfo = new z7Header.PackInfo()
                    {
                        NumPackStreams = numPackStreams,
                        PackPos = 0,
                        Sizes = new UInt64[numPackStreams],
                        Digests = new z7Header.Digests(numPackStreams)
                    };
                    streamsInfo.UnPackInfo = new z7Header.UnPackInfo()
                    {
                        NumFolders = streamIndex,
                        Folders = new z7Header.Folder[streamIndex]
                    };
                    for (ulong i = 0, k = 0; i < streamIndex; ++i)
                    {
                        for (ulong j = 0; j < css[i].NumStreams; ++j, ++k)
                        {
                            streamsInfo.PackInfo.Sizes[k] = css[i].Sizes[j];
                            streamsInfo.PackInfo.Digests.CRCs[k] = css[i].CRCs[j];
                        }
                        streamsInfo.UnPackInfo.Folders[i] = css[i].Folder;
                    }
                }

                // copy filled streams info to main header
                this.header.RawHeader.MainStreamsInfo = streamsInfo;
            }
            finally
            {
                Trace.Unindent();
                Trace.TraceInformation("Done compressing files.");
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
                var sc = new z7StreamsCompressor(stream);
                sc.Codec = Compress.Codec.Query(new Compress.CodecID(0x03, 0x01, 0x01));

                headerStream.Position = 0;
                z7StreamsCompressor.CompressedStream cs = sc.Compress(headerStream);

                z7Header.StreamsInfo headerStreamsInfo = new z7Header.StreamsInfo()
                {
                    PackInfo = new z7Header.PackInfo()
                    {
                        NumPackStreams = cs.NumStreams,
                        PackPos = (UInt64)(endOfPackedStreamsPosition - Marshal.SizeOf(typeof(z7Archive.SignatureHeader))),
                        Sizes = cs.Sizes,
                        Digests = new z7Header.Digests(1)
                        {
                            CRCs = cs.CRCs
                        }
                    },
                    UnPackInfo = new z7Header.UnPackInfo()
                    {
                        Folders = new z7Header.Folder[1] { cs.Folder },
                        NumFolders = 1
                    }
                };
                header.RawHeader = null;
                header.EncodedHeader = headerStreamsInfo;

                headerStream.Dispose();
                headerStream = new MemoryStream();
                header.Write(headerStream);

                endOfPackedStreamsPosition = this.stream.Position;
            }

            // create start header and calculate header crc
            headerStream.Position = 0;
            var startHeader = new z7Archive.StartHeader()
            {
                NextHeaderOffset = (UInt64)(endOfPackedStreamsPosition - Marshal.SizeOf(typeof(z7Archive.SignatureHeader))),
                NextHeaderSize = (UInt64)headerStream.Length,
                NextHeaderCRC = CRC.Calculate(headerStream)
            };

            // write headers at the end of output stream
            headerStream.Position = 0;
            headerStream.CopyTo(stream);

            // regenerate signature header with positions and crcs
            var signatureHeader = new z7Archive.SignatureHeader()
            {
                Signature = z7Archive.kSignature.ToArray(),
                ArchiveVersion = new z7Archive.ArchiveVersion()
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

            bool[] emptyStreams = new bool[Files.LongLength];
            UInt64 numEmptyStreams = 0;
            bool[] emptyFiles = new bool[Files.LongLength];
            UInt64 numEmptyFiles = 0;
            for (long i = 0, j = 0; i < Files.LongLength; ++i)
            {
                var file = Files[i];
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

            var propertyEmptyStream = new z7Header.PropertyEmptyStream((ulong)Files.LongLength)
            {
                IsEmptyStream = emptyStreams,
                NumEmptyStreams = numEmptyStreams
            };
            var propertyEmptyFile = new z7Header.PropertyEmptyFile((ulong)Files.LongLength, numEmptyStreams)
            {
                IsEmptyFile = emptyFiles
            };
            var propertyName = new z7Header.PropertyName((ulong)Files.LongLength)
            {
                Names = new string[Files.LongLength]
            };
            var propertyTime = new z7Header.PropertyTime(z7Header.PropertyID.kMTime, (ulong)Files.LongLength)
            {
                Times = new DateTime[Files.LongLength],
                Defined = Enumerable.Repeat((bool)true, (int)Files.LongLength).ToArray(),
                NumDefined = (ulong)Files.LongLength
            };
            var propertyAttr = new z7Header.PropertyAttributes((ulong)Files.LongLength)
            {
                Attributes = new UInt32[Files.LongLength],
                Defined = Enumerable.Repeat((bool)true, (int)Files.LongLength).ToArray(),
                NumDefined = (ulong)Files.LongLength
            };
            for (long i = 0; i < Files.LongLength; ++i)
            {
                propertyName.Names[i] = Files[i].Name;
                propertyTime.Times[i] = Files[i].Time;
                propertyAttr.Attributes[i] = Files[i].Attributes;
            }

            // create header and add FilesInfo elements

            var header = this.header.RawHeader;
            header.FilesInfo = new z7Header.FilesInfo()
            {
                NumFiles = (ulong)Files.LongLength,
                NumEmptyStreams = numEmptyStreams
            };
            if (numEmptyStreams > 0)
                header.FilesInfo.Properties.Add(propertyEmptyStream);
            if (numEmptyFiles > 0)
                header.FilesInfo.Properties.Add(propertyEmptyFile);
            header.FilesInfo.Properties.AddRange(new z7Header.FileProperty[] { propertyName, propertyTime, propertyAttr });
        }
    }
}
