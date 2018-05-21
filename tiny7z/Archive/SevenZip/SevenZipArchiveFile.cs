using pdj.tiny7z.Common;
using System;

namespace pdj.tiny7z.Archive
{
    public class SevenZipArchiveFile : ArchiveFile
    {
        public UInt64? UnPackIndex;
        public MultiFileStream.Source Source;
        public SevenZipArchiveFile()
            : base()
        {
            this.UnPackIndex = null;
            this.Source = null;
        }
    }
}
