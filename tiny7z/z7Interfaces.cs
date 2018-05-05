using System.IO;

namespace pdj.tiny7z
{
    /// <summary>
    /// Header parser interface (for 7zip header)
    /// </summary>
    public interface IHeaderParser
    {
        void Parse(Stream headerStream);
    }

    /// <summary>
    /// Header writer interface (for 7zip header)
    /// </summary>
    public interface IHeaderWriter
    {
        void Write(Stream headerStream);
    }
}
