using System;
using System.IO;

namespace pdj.tiny7z.Archive
{
    /// <summary>
    /// Extractor proxy interface
    /// </summary>
    public interface IExtractor
    {
        /// <summary>
        /// Set this to true and existing files will be overwritten.
        /// </summary>
        bool OverwriteExistingFiles
        {
            get; set;
        }

        /// <summary>
        /// Set this to true and existing files will not be deleted, but no exception will be thrown.
        /// </summary>
        bool SkipExistingFiles
        {
            get; set;
        }

        /// <summary>
        /// Set this to true so that files are extracted to the directory specified in archive.
        /// </summary>
        bool PreserveDirectoryStructure
        {
            get; set;
        }

        /// <summary>
        /// Set this to true and "anti" files in archives will actively delete files in output.
        /// </summary>
        bool AllowFileDeletions
        {
            get; set;
        }

        /// <summary>
        /// Extracts complete archive to specified directory.
        /// </summary>
        IExtractor ExtractArchive(string outputDirectory);

        /// <summary>
        /// Extracts complete archive, calling the onExtract delegate on each file extraction.
        /// </summary>
        IExtractor ExtractArchive(Func<ArchiveFile, Stream> onStreamRequest, Action<ArchiveFile, Stream> onStreamClose = null);

        /// <summary>
        /// Extracts single file from archive into specified directory.
        /// </summary>
        IExtractor ExtractFile(string fileName, string outputDirectory);

        /// <summary>
        /// Extracts a single file into given stream.
        /// </summary>
        IExtractor ExtractFile(string fileName, Stream outputStream);

        /// <summary>
        /// Extracts a single file identified by its index in file list, into given output directory.
        /// </summary>
        IExtractor ExtractFile(UInt64 index, string outputDirectory);

        /// <summary>
        /// Extracts a single file identified by its index in file list, ito given stream.
        /// </summary>
        IExtractor ExtractFile(UInt64 index, Stream outputStream);

        /// <summary>
        /// Extract multiple files identified by their filenames, into specified directory.
        /// </summary>
        IExtractor ExtractFiles(string[] fileNames, string outputDirectory);

        /// <summary>
        /// Extract multiple files identified by their filenames and call delegate for each file.
        /// </summary>
        IExtractor ExtractFiles(string[] fileNames, Func<ArchiveFile, Stream> onStreamRequest, Action<ArchiveFile, Stream> onStreamClose = null);

        /// <summary>
        /// Extract multiple files identified by their index in file list, into specified output directory.
        /// </summary>
        IExtractor ExtractFiles(UInt64[] indices, string outputDirectory);

        /// <summary>
        /// Extract multiple files identified by their index in file list and call delegate for each file.
        /// </summary>
        IExtractor ExtractFiles(UInt64[] indices, Func<ArchiveFile, Stream> onStreamRequest, Action<ArchiveFile, Stream> onStreamClose = null);
    }

    /// <summary>
    /// Compressor proxy interface
    /// </summary>
    public interface ICompressor
    {
        /// <summary>
        /// Set this to true to keep input files directories into archive.
        /// </summary>
        bool PreserveDirectoryStructure
        {
            get; set;
        }

        /// <summary>
        /// Set this to true to compress all files in a single block (if archive supports it).
        /// </summary>
        bool Solid
        {
            get; set;
        }

        /// <summary>
        /// Set this to true to compress header as well (if archive supports it).
        /// </summary>
        bool CompressHeader
        {
            get; set;
        }

        /// <summary>
        /// Adds a directory content to current archive.
        /// </summary>
        /// <param name="inputDirectory">Input directory to scan files from.</param>
        /// <param name="archiveDirectory">Additional path to prepend to files into archive</param>
        /// <param name="recursive">If true, files from subdirectories are also added.</param>
        ICompressor AddDirectory(string inputDirectory, string archiveDirectory = null, bool recursive = true);

        /// <summary>
        /// Add a single file to archive.
        /// </summary>
        /// <param name="inputFileName">File to add to archive.</param>
        /// <param name="archiveFileName">If specified, this is the new name of the file once into the archive.</param>
        ICompressor AddFile(string inputFileName, string archiveFileName = null);

        /// <summary>
        /// Add a single file to archive, but from a given stream.
        /// </summary>
        /// <param name="stream">Stream containing file data.</param>
        /// <param name="archiveFileName">Name of file once into the archive.</param>
        /// <param name="time">If specified, time of file to set into the archive.</param>
        ICompressor AddFile(Stream stream, string archiveFileName, DateTime? time = null);

        /// <summary>
        /// This has to be called after all `Add` operations have been done to actually complete the archive, including writing headers. Without calling this, actual compressed data is in the file, but no index is created.
        /// </summary>
        ICompressor Finalize();
    }

    /// <summary>
    /// Header parser interface
    /// </summary>
    public interface IHeaderParser
    {
        void Parse(Stream headerStream);
    }

    /// <summary>
    /// Header writer interface
    /// </summary>
    public interface IHeaderWriter
    {
        void Write(Stream headerStream);
    }
}
