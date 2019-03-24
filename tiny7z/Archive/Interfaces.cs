using pdj.tiny7z.Common;
using System;
using System.Collections.Generic;
using System.IO;

namespace pdj.tiny7z.Archive
{
    /// <summary>
    /// Possible results of a feedback request
    /// </summary>
    public enum FeedbackResult
    {
        Yes,
        No,
        Cancel
    }

    /// <summary>
    /// This delegate can be used to provide feedback in the middle of a running de/compression action
    /// </summary>
    /// <param name="files">List of files that are requiring feedback to continue</param>
    /// <returns>TRUE or FALSE depending on received feedback</returns>
    public delegate FeedbackResult FeedbackNeededDelegate (IEnumerable<ArchiveFile> files);

    /// <summary>
    /// User progress delegate called by IProgressProvider implementation
    /// </summary>
    /// <param name="provider">Reference to the IProgressProvider object calling this. Allows accessing the list of files</param>
    /// <param name="included">This will be set to TRUE if current file is being de/compressed, or if it's just being processed to get to other files.</param>
    /// <param name="currentFileIndex">Index of file referenced from the list of files</param>
    /// <param name="currentFileSize">Current size of file having been processed</param>
    /// <param name="filesSize">Current size of cumulative files having been processed</param>
    /// <param name="rawSize">Current size of total data having been processed</param>
    /// <param name="compressedSize">Compressed size of data. If unavailable, this will be ZERO</param>
    /// <returns>TRUE if everything is fine, FALSE if processing should be aborted if possible</returns>
    public delegate bool ProgressDelegate(IProgressProvider provider, bool included, int currentFileIndex, ulong currentFileSize, ulong filesSize, ulong rawSize, ulong compressedSize);

    /// <summary>
    /// Progress feedback interface
    /// </summary>
    public interface IProgressProvider
    {
        /// <summary>
        /// List of files that are being processed and targetted by the progress report
        /// </summary>
        IReadOnlyList<ArchiveFile> Files
        {
            get;
        }

        /// <summary>
        /// Actual progress delegate that will be called everytime this object is being updated
        /// </summary>
        ProgressDelegate ProgressFunc
        {
            get; set;
        }

        /// <summary>
        /// Total size of archive, including all files.
        /// </summary>
        UInt64 RawTotalSize
        {
            get;
        }

        /// <summary>
        /// Total size of selected files from archive.
        /// </summary>
        UInt64 TotalSize
        {
            get;
        }

        /// <summary>
        /// File processing methods will call this to increase base offset. When compressor/decompressor is called mutliple times for the same compression/decompression operation, this ensures a cumulative process
        /// </summary>
        /// <param name="rawSizeOffset">Raw size offset</param>
        /// <param name="compressedSizeOffset">Compressed size offset</param>
        void IncreaseOffsetBy(long rawSizeOffset, long compressedSizeOffset);

        /// <summary>
        /// File processing methods will call this to notify of progress
        /// </summary>
        /// <param name="rawSize">Raw size progress</param>
        /// <param name="compressedSize">Compressed size progress</param>
        /// <returns></returns>
        bool SetProgress(ulong rawSize, ulong compressedSize);
    }

    /// <summary>
    /// Extractor proxy interface
    /// </summary>
    public interface IExtractor : IDisposable
    {
        /// <summary>
        /// List of files contained in the opened archive.
        /// </summary>
        IReadOnlyList<ArchiveFile> Files
        {
            get;
        }

        /// <summary>
        /// Function that can be called to show progress.
        /// </summary>
        ProgressDelegate ProgressDelegate
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
        /// Set this to true and existing files will be overwritten.
        /// </summary>
        bool OverwriteExistingFiles
        {
            get; set;
        }

        /// <summary>
        /// Set this to enable encrypted password-protected extraction.
        /// </summary>
        string Password
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
        /// Set this to true and existing files will not be deleted, but no exception will be thrown.
        /// </summary>
        bool SkipExistingFiles
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

        /// <summary>
        /// When this is called, internal values are cleared and the extractor cannot be used anymore.
        /// </summary>
        IExtractor Finalize();
    }

    /// <summary>
    /// Compressor proxy interface
    /// </summary>
    public interface ICompressor : IDisposable
    {
        /// <summary>
        /// List of files in the archive.
        /// </summary>
        IReadOnlyList<ArchiveFile> Files
        {
            get;
        }

        /// <summary>
        /// Function that can be called to show progress.
        /// </summary>
        ProgressDelegate ProgressDelegate
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
    internal interface IHeaderParser
    {
        void Parse(Stream headerStream);
    }

    /// <summary>
    /// Header writer interface
    /// </summary>
    internal interface IHeaderWriter
    {
        void Write(Stream headerStream);
    }
}
