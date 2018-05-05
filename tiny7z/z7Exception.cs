using System;

namespace pdj.tiny7z
{
    /// <summary>
    /// Base exception class for error handling
    /// </summary>
    public class z7Exception : Exception
    {
        public z7Exception(string message)
            : base(message)
        {
        }
    }
}
