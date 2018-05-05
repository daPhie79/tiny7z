using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pdj.tiny7z.Archive
{
    public interface Archive
    {
        bool IsValid
        {
            get;
        }
        IExtractor Extractor();
        ICompressor Compressor();
    }

    public interface IExtractor
    {
        IExtractor ExtractAll(string outputPath, bool overwriteExistingFiles = false);
    }

    public interface ICompressor
    {
        ICompressor CompressAll(string inputPath, bool recursive = true);
    }
}
