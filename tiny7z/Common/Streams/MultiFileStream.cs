using System;
using System.IO;

namespace pdj.tiny7z.Common
{
    /// <summary>
    /// MultiFileStream - Allows treating a bunch of files sequentially to behave as if they're one stream.
    /// </summary>
    public class MultiFileStream : AbstractMultiStream
    {
        /// <summary>
        /// Multi-purpose container class. Holds either a file path or an already opened stream.
        /// </summary>
        public class Source
        {
            public Stream Get(FileAccess fileAccess)
            {
                Stream s = null;
                if (this.stream != null)
                {
                    s = this.stream;
                    if ((fileAccess == FileAccess.Read && !s.CanRead) || (fileAccess == FileAccess.Write && !s.CanWrite))
                        throw new IOException();
                }
                else if (this.filePath != null)
                {
                    if (fileAccess == FileAccess.Read)
                        s = File.Open(this.filePath, FileMode.Open, FileAccess.Read);
                    else
                        s = File.Open(this.filePath, FileMode.Create, FileAccess.Write);
                }
                Clear();
                return s;
            }

            public long Size()
            {
                if (this.stream != null)
                    return this.stream.Length;
                else if (this.filePath != null)
                    return new FileInfo(this.filePath).Length;
                return -1;
            }

            public Source Set(Stream stream)
            {
                this.stream = stream;
                this.filePath = null;
                return this;
            }

            public Source Set(string filePath)
            {
                this.stream = null;
                this.filePath = filePath;
                return this;
            }

            public Source Clear()
            {
                this.stream = null;
                this.filePath = null;
                return this;
            }

            public Source()
            {
                this.stream = null;
                this.filePath = null;
            }

            public Source(string FilePath)
            {
                this.stream = null;
                this.filePath = FilePath;
            }

            public Source(Stream Stream)
            {
                this.stream = Stream;
                this.filePath = null;
            }

            Stream stream;
            string filePath;
        }

        /// <summary>
        /// Overridden method returns source from list as next stream!
        /// </summary>
        protected override Stream NextStream()
        {
            return this.Sources[currentIndex].Get(this.fileAccess);
        }

        /// <summary>
        /// List of sources
        /// </summary>
        public Source[] Sources
        {
            get; private set;
        }

        /// <summary>
        /// Straightforward stream initialization.
        /// </summary>
        public MultiFileStream(FileAccess fileAccess, params Source[] sources)
            : base((ulong)sources.LongLength)
        {
            if (fileAccess == FileAccess.ReadWrite)
                throw new ArgumentException();
            if (sources == null || sources.Length == 0)
                throw new ArgumentOutOfRangeException();

            this.fileAccess = fileAccess;
            this.Sources = sources;
            for (long i = 0; i < sources.LongLength; ++i)
            {
                this.Sizes[i] = Sources[i].Size();
            }
        }

        /// <summary>
        /// Remember either read or write access.
        /// </summary>
        private FileAccess fileAccess;
    }
}
