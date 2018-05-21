using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pdj.tiny7z.Archive
{
    public abstract class Archive
    {
        public virtual bool IsValid
        {
            get; protected set;
        }
        public abstract IExtractor Extractor();
        public abstract ICompressor Compressor();
    }
}
