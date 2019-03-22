using ManagedLzma.LZMA.Master;
using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace pdj.tiny7z.Archive
{
    class SevenZipProgressProvider : IProgressProvider, LZMA.ICompressProgress
    {
        #region Public Properties (IProgressProvider)
        public IReadOnlyList<ArchiveFile> Files
        {
            get; private set;
        }

        public ProgressDelegate ProgressFunc
        {
            get; set;
        }

        public UInt64 RawTotalSize
        {
            get; private set;
        }

        public UInt64 TotalSize
        {
            get; private set;
        }
        #endregion Public Properties

        #region Public Methods (IProgressProvider)
        public void IncreaseOffsetBy(long rawSizeOffset, long compressedSizeOffset)
        {
            rawOffset = checked((ulong)((long)rawOffset + compressedSizeOffset));
            compressedOffset = checked((ulong)((long)compressedOffset + compressedSizeOffset));
        }

        public bool SetProgress(ulong rawSize, ulong compressedSize)
        {
            // only do anything if there's a progress delegate

            if (ProgressFunc != null)
            {
                // locate current file

                for (; lastFileIndex < files.Count; lastFileIndex++)
                {
                    if (lastRawOffset + (files[lastFileIndex].Size ?? 0) > (rawOffset + rawSize))
                    {
                        break;
                    }
                    lastRawOffset += files[lastFileIndex].Size ?? 0;
                    if (indices == null || indices.IndexOf((ulong)lastFileIndex) != -1)
                        lastFileOffset += files[lastFileIndex].Size ?? 0;
                }

                // call progress delegate

                ulong currentFileSize = rawOffset + rawSize - lastRawOffset;
                return ProgressFunc(
                    this,
                    lastFileIndex,
                    currentFileSize,
                    lastFileOffset + currentFileSize,
                    rawOffset + rawSize,
                    compressedOffset + compressedSize);
            }
            return true;
        }
        #endregion Public Methods (IProgressProvider)

        #region Public Methods (LZMA.ICompressProgress)
        public LZMA.SRes Progress(ulong inSize, ulong outSize)
        {
            return SetProgress(inSize, outSize) ? LZMA.SZ_OK : LZMA.SZ_ERROR_PROGRESS;
        }
        #endregion Public Methods (LZMA.ICompressProgress)

        #region Public Constructor
        public SevenZipProgressProvider(IList<SevenZipArchiveFile> files, IList<UInt64> indices, ProgressDelegate progressFunc = null)
        {
            this.files = files;
            this.indices = indices;
            Files = new ReadOnlyCollection<SevenZipArchiveFile>(this.files);
            ProgressFunc = progressFunc;
            RawTotalSize = (ulong)this.files.Sum(f => (decimal)(f.Size ?? 0));
            TotalSize = this.indices == null ? RawTotalSize : (ulong)this.indices.Select(i => this.files[(int)i]).Sum(f => (decimal)(f.Size ?? 0));
        }
        #endregion

        #region Private Fields
        IList<SevenZipArchiveFile> files;
        IList<UInt64> indices;
        int lastFileIndex = 0;
        ulong lastFileOffset = 0;
        ulong rawOffset = 0;
        ulong lastRawOffset = 0;
        ulong compressedOffset = 0;
        #endregion Private Fields
    }
}
