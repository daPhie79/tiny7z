using pdj.tiny7z.Archive;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pdj.tiny7z
{
    public class z7ArchiveFile : ArchiveFile
    {
        public Int64 UnPackIndex;
        public string InputPath;
        public z7ArchiveFile()
            : base()
        {
            this.UnPackIndex = -1;
            this.InputPath = string.Empty;
        }
    }
}
