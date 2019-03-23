using pdj.tiny7z.Common;
using System;

namespace pdj.tiny7z.Archive
{
    /// <summary>
    /// Base exception class for error handling
    /// </summary>
    public class SevenZipException : Exception
    {
        internal SevenZipException(string message)
            : base(message)
        {
        }
    }
}
