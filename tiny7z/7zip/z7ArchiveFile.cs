using pdj.tiny7z.Archive;
using pdj.tiny7z.Common;
using System;


namespace pdj.tiny7z
{
    public class z7ArchiveFile : ArchiveFile
    {
        public UInt64? UnPackIndex;
        public MultiFileStream.Source Source;
        public z7ArchiveFile()
            : base()
        {
            this.UnPackIndex = null;
            this.Source = null;
        }
    }
}
