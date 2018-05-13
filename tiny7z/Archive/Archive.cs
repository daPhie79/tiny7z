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
}
