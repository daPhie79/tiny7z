using ManagedLzma.LZMA.Master;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace pdj.tiny7z.Archive
{
    class SevenZipProgressProvider : IProgressProvider, LZMA.ICompressProgress
    {
        #region Public Properties (IProgressProvider)
        public IReadOnlyCollection<ArchiveFile> Files
        {
            get; private set;
        }

        public ProgressDelegate ProgressFunc
        {
            get; set;
        }
        #endregion Public Properties

        #region Public Methods
        // IProgressProvider
        public void IncreaseOffsetBy(long rawSizeOffset, long compressedSizeOffset)
        {
            rawOffset = checked((ulong)((long)rawOffset + compressedSizeOffset));
            compressedOffset = checked((ulong)((long)compressedOffset + compressedSizeOffset));
        }

        public bool SetProgress(ulong rawSize, ulong compressedSize)
        {
            if (ProgressFunc != null)
            {
                return ProgressFunc(this, 0, 0, rawOffset + rawSize, 0, compressedOffset + compressedSize);
            }
            return true;
        }

        // LZMA.ICompressProgress (SevenZipStreamsCompression)
        public LZMA.SRes Progress(ulong inSize, ulong outSize) => 
            SetProgress(inSize, outSize) ? LZMA.SZ_OK : LZMA.SZ_ERROR_PROGRESS;
        #endregion

        #region Public Constructor
        public SevenZipProgressProvider(IList<SevenZipArchiveFile> files)
        {
            Files = new ReadOnlyCollection<SevenZipArchiveFile>(this.files = files);
            ProgressFunc = null;
        }
        #endregion

        #region Private Fields
        IList<SevenZipArchiveFile> files;
        ulong rawOffset = 0;
        ulong compressedOffset = 0;
        #endregion Private Fields
    }
}
