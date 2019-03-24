using pdj.tiny7z.Common;
using System;

namespace pdj.tiny7z.Archive
{
    public class SevenZipException : Exception
    {
        internal SevenZipException(string message)
            : base(message)
        {
        }
    }

    public class SevenZipFileAlreadyExistsException : SevenZipException
    {
        internal SevenZipFileAlreadyExistsException(SevenZipArchiveFile file)
            : base($"File `{file.Name}` already exists.")
        {
        }
    }

    public class SevenZipPasswordRequiredException : SevenZipException
    {
        internal SevenZipPasswordRequiredException()
            : base("No password provided. Encrypted stream requires password.")
        {
        }
    }
}
