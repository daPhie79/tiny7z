using System;

namespace pdj.tiny7z.Archive
{
    /// <summary>
    /// Represents one file in an archive
    /// </summary>
    public class ArchiveFile
    {
        public string Name;
        public UInt64 Size;
        public UInt32 CRC;
        public DateTime Time;
        public UInt32 Attributes;
        public bool IsEmpty;
        public bool IsDirectory;
        public bool IsDeleted;
        public ArchiveFile()
        {
            Name = string.Empty;
            Size = 0;
            CRC = 0;
            Time = DateTime.Now;
            Attributes = 0;
            IsEmpty = false;
            IsDirectory = false;
            IsDeleted = false;
        }
    }
}
