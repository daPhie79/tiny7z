using pdj.tiny7z.Common;
using System;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;

namespace pdj.tiny7z.Archive
{
    /// <summary>
    /// Main 7zip archive class to handle reading and writing into .7z archive files.
    /// </summary>
    public class SevenZipArchive : Archive, IDisposable
    {
        #region Public Constants and Structs
        /// <summary>
        /// 7zip file signature
        /// </summary>
        internal static readonly Byte[] kSignature = new Byte[6] { (Byte)'7', (Byte)'z', 0xBC, 0xAF, 0x27, 0x1C };

        /// <summary>
        /// 7zip file archive version
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct ArchiveVersion
        {
            [MarshalAs(UnmanagedType.U1)]
            public Byte Major;   // now = 0
            [MarshalAs(UnmanagedType.U1)]
            public Byte Minor;   // now = 2
        };

        /// <summary>
        /// Header part that tells where the actual header starts at the end of the file usually
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct StartHeader
        {
            [MarshalAs(UnmanagedType.U8)]
            public UInt64 NextHeaderOffset;

            [MarshalAs(UnmanagedType.U8)]
            public UInt64 NextHeaderSize;

            [MarshalAs(UnmanagedType.U4)]
            public UInt32 NextHeaderCRC;
        }

        /// <summary>
        /// Signature header of a valid 7zip file
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct SignatureHeader
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public Byte[] Signature;

            public ArchiveVersion ArchiveVersion;

            [MarshalAs(UnmanagedType.U4)]
            public UInt32 StartHeaderCRC;

            public StartHeader StartHeader;
        }
        #endregion Public Constants and Structs

        #region Internal Properties
        internal SevenZipHeader Header
        {
            get; set;
        }
        #endregion Internal Properties

        #region Private Fields
        SignatureHeader signatureHeader;
        Stream stream;
        FileAccess? fileAccess;
        #endregion Private Fields

        #region Public Constructors
        /// <summary>
        /// Construct a 7zip file with an existing stream
        /// </summary>
        public SevenZipArchive(Stream stream, FileAccess fileAccess)
            : this()
        {
            this.stream = stream;
            this.fileAccess = fileAccess;
            if (fileAccess == FileAccess.Read)
            {
                Trace.TraceInformation("Open 7zip archive for reading.");
                open();
            }
            else if (fileAccess == FileAccess.Write)
            {
                Trace.TraceInformation("Open 7zip archive for writing.");
                create();
            }
            else
            {
                throw new ArgumentException("`fileAccess` must be either `Read` or `Write`.");
            }
        }
        #endregion Public Constructors

        #region Public Methods
        /// <summary>
        /// IDiposable interface requirement
        /// </summary>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// Returns an extractor object to retrieve files from
        /// </summary>
        /// <returns></returns>
        public override IExtractor Extractor()
        {
            if (this.stream == null)
                throw new SevenZipException("SevenZipArchive object is either uninitialized or closed.");
            return new SevenZipExtractor(stream, Header);
        }

        /// <summary>
        /// Returns a compressor object to compress files into
        /// </summary>
        /// <returns></returns>
        public override ICompressor Compressor()
        {
            if (this.stream == null)
                throw new SevenZipException("SevenZipArchive object is either uninitialized or closed.");
            return new SevenZipCompressor(stream, Header);
        }

        /// <summary>
        /// Close current archive and stream. Archive is committed after this
        /// </summary>
        public void Close()
        {
            this.signatureHeader = new SignatureHeader();
            if (this.stream != null)
            {
                this.stream.Close();
                this.stream.Dispose();
                this.stream = null;
            }
            this.fileAccess = null;
            this.Header = null;
            this.IsValid = false;
        }

        /// <summary>
        /// Dump debug information to console
        /// </summary>
        public void Dump()
        {
            // TODO
        }
        #endregion Public Methods

        #region Private Constructors
        /// <summary>
        /// Defaut empty constructor
        /// </summary>
        private SevenZipArchive()
        {
            stream = null;
            fileAccess = null;
            Header = null;
            IsValid = false;
        }
        #endregion Private Constructors

        #region Private Methods
        /// <summary>
        /// Open an existing 7zip file for reading
        /// </summary>
        private void open()
        {
            SignatureHeader sig = stream.ReadStruct<SignatureHeader>();
            if (!sig.Signature.SequenceEqual(kSignature))
            {
                throw new SevenZipException("File is not a valid 7zip file.");
            }
            this.signatureHeader = sig;

            // some debug info

            Trace.TraceInformation("Opening 7zip file:");
            Trace.Indent();

            try
            {
                Trace.TraceInformation($"Version: {sig.ArchiveVersion.Major}.{sig.ArchiveVersion.Minor}");
                Trace.TraceInformation($"StartHeaderCRC: {sig.StartHeaderCRC.ToString("X8")}");
                Trace.TraceInformation($"NextHeaderOffset: {sig.StartHeader.NextHeaderOffset}");
                Trace.TraceInformation($"NextHeaderCRC: {sig.StartHeader.NextHeaderCRC.ToString("X8")}");
                Trace.TraceInformation($"NextHeaderSize: {sig.StartHeader.NextHeaderSize}");
                Trace.TraceInformation($"All headers: " + (sig.StartHeader.NextHeaderSize + (uint)Marshal.SizeOf(sig)) + " bytes");

                {
                    uint crc32 = new CRC().Calculate(sig.StartHeader.GetByteArray()).Result;
                    if (crc32 != sig.StartHeaderCRC)
                    {
                        throw new SevenZipException("StartHeaderCRC mismatch: " + crc32.ToString("X8"));
                    }
                }

                // buffer header in memory for further processing

                byte[] buffer = new byte[sig.StartHeader.NextHeaderSize];
                stream.Seek((long)sig.StartHeader.NextHeaderOffset, SeekOrigin.Current);
                if (stream.Read(buffer, 0, (int)sig.StartHeader.NextHeaderSize) != (int)sig.StartHeader.NextHeaderSize)
                {
                    throw new SevenZipException("Reached end of file before end of header.");
                }

                {
                    uint crc32 = new CRC().Calculate(buffer).Result;
                    if (crc32 != sig.StartHeader.NextHeaderCRC)
                    {
                        throw new SevenZipException("StartHeader.NextHeaderCRC mismatch: " + crc32.ToString("X8"));
                    }
                }

                // initiate header parsing

                Trace.TraceInformation("Parsing 7zip file header:");
                Header = new SevenZipHeader(new MemoryStream(buffer));
                Header.Parse();

                // decompress encoded header if found

                if (Header.RawHeader == null && Header.EncodedHeader != null)
                {
                    Trace.TraceInformation("Encoded header detected, decompressing.");
                    Stream newHeaderStream = new MemoryStream();
                    (new SevenZipStreamsExtractor(stream, Header.EncodedHeader)).Extract(0, newHeaderStream, null);

                    Trace.TraceInformation("Parsing decompressed header:");
                    newHeaderStream.Position = 0;
                    SevenZipHeader
                        newHeader = new SevenZipHeader(newHeaderStream);
                        newHeader.Parse();
                    Header = newHeader;
                }

                IsValid = true;
            }
            finally
            {
                Trace.Unindent();
                Trace.TraceInformation("Done parsing 7zip file header.");
            }
        }

        /// <summary>
        /// Create a new 7zip file for writing
        /// </summary>
        private void create()
        {
            this.signatureHeader = new SignatureHeader()
            {
                Signature = kSignature.ToArray(),
                ArchiveVersion = new ArchiveVersion()
                {
                    Major = 0,
                    Minor = 2,
                },
            };
            stream.Write(this.signatureHeader.GetByteArray(), 0, Marshal.SizeOf(this.signatureHeader));

            this.Header = new SevenZipHeader(null, true);
        }
        #endregion Private Methods
    }
}
