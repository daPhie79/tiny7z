using pdj.tiny7z.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Text;

namespace pdj.tiny7z.Archive
{
    internal partial class SevenZipHeader : IHeaderParser, IHeaderWriter
    {
        #region Internal Enums
        /// <summary>
        /// All valid property IDs
        /// </summary>
        internal enum PropertyID
        {
            kEnd = 0x00,

            kHeader = 0x01,

            kArchiveProperties = 0x02,

            kAdditionalStreamsInfo = 0x03,
            kMainStreamsInfo = 0x04,
            kFilesInfo = 0x05,

            kPackInfo = 0x06,
            kUnPackInfo = 0x07,
            kSubStreamsInfo = 0x08,

            kSize = 0x09,
            kCRC = 0x0A,

            kFolder = 0x0B,

            kCodersUnPackSize = 0x0C,
            kNumUnPackStream = 0x0D,

            kEmptyStream = 0x0E,
            kEmptyFile = 0x0F,
            kAnti = 0x10,

            kName = 0x11,
            kCTime = 0x12,
            kATime = 0x13,
            kMTime = 0x14,
            kWinAttributes = 0x15,
            kComment = 0x16,

            kEncodedHeader = 0x17,

            kStartPos = 0x18,
            kDummy = 0x19,
        };
        #endregion Internal Enums

        #region Internal Classes
        internal class Digests : IHeaderParser, IHeaderWriter
        {
            public UInt64 NumStreams()
            {
                return (UInt64)CRCs.LongLength;
            }
            public UInt64 NumDefined()
            {
                return (UInt64)CRCs.Count(crc => crc != null);
            }
            public bool Defined(UInt64 index)
            {
                return CRCs[index] != null;
            }
            public UInt32?[] CRCs;
            public Digests(UInt64 NumStreams)
            {
                CRCs = new UInt32?[NumStreams];
            }

            public void Parse(Stream hs)
            {
                bool[] defined;
                var numDefined = hs.ReadOptionalBoolVector(NumStreams(), out defined);

                using (BinaryReader reader = new BinaryReader(hs, Encoding.Default, true))
                    for (long i = 0; i < defined.LongLength; ++i)
                        if (defined[i])
                            CRCs[i] = reader.ReadUInt32();
            }

            public void Write(Stream hs)
            {
                bool[] defined = CRCs.Select(crc => (bool)(crc != null)).ToArray();
                hs.WriteOptionalBoolVector(defined);

                using (BinaryWriter writer = new BinaryWriter(hs, Encoding.Default, true))
                    for (ulong i = 0; i < NumStreams(); ++i)
                        if (CRCs[i] != null)
                            writer.Write((UInt32)CRCs[i]);
            }
        }

        internal class ArchiveProperty : IHeaderParser, IHeaderWriter
        {
            public PropertyID Type;
            public UInt64 Size;
            public Byte[] Data;
            public ArchiveProperty(PropertyID type)
            {
                this.Type = type;
                this.Size = 0;
                this.Data = new Byte[0];
            }

            public void Parse(Stream hs)
            {
                Size = hs.ReadDecodedUInt64();
                if (Size > 0)
                    Data = hs.ReadThrow(Size);
            }

            public void Write(Stream hs)
            {
                hs.WriteByte((Byte)Type);
                hs.WriteEncodedUInt64(Size);
                if (Size > 0)
                    hs.Write(Data, 0, (int)Size);
            }
        }

        internal class ArchiveProperties : IHeaderParser, IHeaderWriter
        {
            public List<ArchiveProperty> Properties; // [Arbitrary number]
            public ArchiveProperties()
            {
                this.Properties = new List<ArchiveProperty>();
            }

            public void Parse(Stream hs)
            {
                while (true)
                {
                    PropertyID propertyID = GetPropertyID(this, hs);
                    if (propertyID == PropertyID.kEnd)
                        return;

                    ArchiveProperty property = new ArchiveProperty(propertyID);
                    property.Parse(hs);
                    Properties.Add(property);
                }
            }

            public void Write(Stream hs)
            {
                foreach (var property in Properties)
                    property.Write(hs);
                hs.WriteByte((Byte)PropertyID.kEnd);
            }
        }

        internal class PackInfo : IHeaderParser, IHeaderWriter
        {
            public UInt64 PackPos;
            public UInt64 NumPackStreams;
            public UInt64[] Sizes; // [NumPackStreams]
            public Digests Digests; // [NumPackStreams]
            public PackInfo()
            {
                this.PackPos = 0;
                this.NumPackStreams = 0;
                this.Sizes = new UInt64[0];
                this.Digests = new Digests(0);
            }

            public void Parse(Stream hs)
            {
                PackPos = hs.ReadDecodedUInt64();
                NumPackStreams = hs.ReadDecodedUInt64();
                Sizes = new UInt64[NumPackStreams­];
                Digests = new Digests(NumPackStreams);
                while (true)
                {
                    PropertyID propertyID = GetPropertyID(this, hs);
                    switch (propertyID)
                    {
                        case PropertyID.kSize:
                            for (ulong i = 0; i < NumPackStreams; ++i)
                                Sizes[i] = hs.ReadDecodedUInt64();
                            break;
                        case PropertyID.kCRC:
                            Digests.Parse(hs);
                            break;
                        case PropertyID.kEnd:
                            return;
                        default:
                            throw new NotImplementedException(propertyID.ToString());
                    }
                }
            }

            public void Write(Stream hs)
            {
                hs.WriteEncodedUInt64(PackPos);
                hs.WriteEncodedUInt64(NumPackStreams);

                hs.WriteByte((Byte)PropertyID.kSize);
                for (ulong i = 0; i < NumPackStreams; ++i)
                    hs.WriteEncodedUInt64(Sizes[i]);

                if (Digests.NumDefined() > 0)
                {
                    hs.WriteByte((Byte)PropertyID.kCRC);
                    Digests.Write(hs);
                }

                hs.WriteByte((Byte)PropertyID.kEnd);
            }
        }

        internal class CoderInfo : IHeaderParser, IHeaderWriter
        {
            public const Byte AttrSizeMask      = 0b00001111;
            public const Byte AttrComplexCoder  = 0b00010000;
            public const Byte AttrHasAttributes = 0b00100000;
            public Byte Attributes;
            public Byte[] CodecId; // [CodecIdSize]
            public UInt64 NumInStreams;
            public UInt64 NumOutStreams;
            public UInt64 PropertiesSize;
            public Byte[] Properties; // [PropertiesSize]
            public CoderInfo()
            {
                this.Attributes = 0;
                this.CodecId = new Byte[0];
                this.NumInStreams = 0;
                this.NumOutStreams = 0;
                this.PropertiesSize = 0;
                this.Properties = new Byte[0];
            }

            public void Parse(Stream hs)
            {
                Attributes = hs.ReadByteThrow();
                int codecIdSize = (Attributes & AttrSizeMask);
                bool isComplexCoder = (Attributes & AttrComplexCoder) > 0;
                bool hasAttributes = (Attributes & AttrHasAttributes) > 0;

                CodecId = hs.ReadThrow((uint)codecIdSize);

                NumInStreams = NumOutStreams = 1;
                if (isComplexCoder)
                {
                    NumInStreams = hs.ReadDecodedUInt64();
                    NumOutStreams = hs.ReadDecodedUInt64();
                }

                PropertiesSize = 0;
                if (hasAttributes)
                {
                    PropertiesSize = hs.ReadDecodedUInt64();
                    Properties = hs.ReadThrow(PropertiesSize);
                }
            }

            public void Write(Stream hs)
            {
                hs.WriteByte(Attributes);
                int codecIdSize = (Attributes & AttrSizeMask);
                bool isComplexCoder = (Attributes & AttrComplexCoder) > 0;
                bool hasAttributes = (Attributes & AttrHasAttributes) > 0;

                hs.Write(CodecId, 0, codecIdSize);

                if (isComplexCoder)
                {
                    hs.WriteEncodedUInt64(NumInStreams);
                    hs.WriteEncodedUInt64(NumOutStreams);
                }

                if (hasAttributes)
                {
                    hs.WriteEncodedUInt64(PropertiesSize);
                    hs.Write(Properties, 0, (int)PropertiesSize);
                }
            }
        }

        internal class BindPairsInfo : IHeaderParser, IHeaderWriter
        {
            public UInt64 InIndex;
            public UInt64 OutIndex;
            public BindPairsInfo()
            {
                this.InIndex = 0;
                this.OutIndex = 0;
            }

            public void Parse(Stream hs)
            {
                InIndex = hs.ReadDecodedUInt64();
                OutIndex = hs.ReadDecodedUInt64();
            }

            public void Write(Stream hs)
            {
                hs.WriteEncodedUInt64(InIndex);
                hs.WriteEncodedUInt64(OutIndex);
            }
        }

        internal class Folder : IHeaderParser, IHeaderWriter
        {
            public UInt64 NumCoders;
            public CoderInfo[] CodersInfo;
            public UInt64 NumInStreamsTotal;
            public UInt64 NumOutStreamsTotal;
            public UInt64 NumBindPairs; // NumOutStreamsTotal - 1
            public BindPairsInfo[] BindPairsInfo; // [NumBindPairs]
            public UInt64 NumPackedStreams; // NumInStreamsTotal - NumBindPairs
            public UInt64[] PackedIndices; // [NumPackedStreams]

            #region Added From UnPackInfo (for convenience)
            public UInt64[] UnPackSizes; // [NumOutStreamsTotal]
            public UInt32? UnPackCRC; // NULL is undefined
            #endregion Added From UnPackInfo

            public Folder()
            {
                this.NumCoders = 0;
                this.CodersInfo = new CoderInfo[0];
                this.NumInStreamsTotal = 0;
                this.NumOutStreamsTotal = 0;
                this.NumBindPairs = 0;
                this.BindPairsInfo = new BindPairsInfo[0];
                this.NumPackedStreams = 0;
                this.PackedIndices = new UInt64[0];
                this.UnPackSizes = new UInt64[0];
                this.UnPackCRC = null;
            }

            public void Parse(Stream hs)
            {
                NumCoders = hs.ReadDecodedUInt64();
                CodersInfo = new CoderInfo[NumCoders];
                for (ulong i = 0; i < NumCoders; ++i)
                {
                    CodersInfo[i] = new CoderInfo();
                    CodersInfo[i].Parse(hs);
                    NumInStreamsTotal += CodersInfo[i].NumInStreams;
                    NumOutStreamsTotal += CodersInfo[i].NumOutStreams;
                }

                NumBindPairs = NumOutStreamsTotal - 1;
                BindPairsInfo = new BindPairsInfo[NumBindPairs];
                for (ulong i = 0; i < NumBindPairs; ++i)
                {
                    BindPairsInfo[i] = new BindPairsInfo();
                    BindPairsInfo[i].Parse(hs);
                }

                NumPackedStreams = NumInStreamsTotal - NumBindPairs;
                if (NumPackedStreams > 1)
                {
                    PackedIndices = new UInt64[NumPackedStreams];
                    for (ulong i = 0; i < NumPackedStreams; ++i)
                        PackedIndices[i] = hs.ReadDecodedUInt64();
                }
                else
                    PackedIndices = new UInt64[] { 0 };
            }

            public void Write(Stream hs)
            {
                hs.WriteEncodedUInt64(NumCoders);
                for (ulong i = 0; i < NumCoders; ++i)
                    CodersInfo[i].Write(hs);

                for (ulong i = 0; i < NumBindPairs; ++i)
                    BindPairsInfo[i].Write(hs);

                if (NumPackedStreams > 1)
                    for (ulong i = 0; i < NumPackedStreams; ++i)
                        hs.WriteEncodedUInt64(PackedIndices[i]);
            }

            public UInt64 GetUnPackSize()
            {
                if (UnPackSizes.Length == 0)
                    return 0;

                for (long i = 0; i < UnPackSizes.LongLength; ++i)
                {
                    bool foundBindPair = false;
                    for (ulong j = 0; j < NumBindPairs; ++j)
                    {
                        if (BindPairsInfo[j].OutIndex == (UInt64)i)
                        {
                            foundBindPair = true;
                            break;
                        }
                    }
                    if (!foundBindPair)
                    {
                        return UnPackSizes[i];
                    }
                }

                throw new SevenZipException("Could not find final unpack size.");
            }

            public Int64 FindBindPairForInStream(UInt64 inStreamIndex)
            {
                for (UInt64 i = 0; i < NumBindPairs; ++i)
                    if (BindPairsInfo[i].InIndex == inStreamIndex)
                        return (Int64)i;
                return -1;
            }

            public Int64 FindBindPairForOutStream(UInt64 outStreamIndex)
            {
                for (UInt64 i = 0; i < NumBindPairs; ++i)
                    if (BindPairsInfo[i].OutIndex == outStreamIndex)
                        return (Int64)i;
                return -1;
            }

            public Int64 FindPackedIndexForInStream(UInt64 inStreamIndex)
            {
                for (UInt64 i = 0; i < NumPackedStreams; ++i)
                    if (PackedIndices[i] == inStreamIndex)
                        return (Int64)i;
                return -1;
            }
        }

        internal class UnPackInfo : IHeaderParser, IHeaderWriter
        {
            public UInt64 NumFolders;
            public Byte External;
            public Folder[] Folders; // [NumFolders]
            public UInt64 DataStreamsIndex;
            public UnPackInfo()
            {
                this.NumFolders = 0;
                this.External = 0;
                this.Folders = new Folder[0];
                this.DataStreamsIndex = 0;
            }

            public void Parse(Stream hs)
            {
                ExpectPropertyID(this, hs, PropertyID.kFolder);

                // Folders

                NumFolders = hs.ReadDecodedUInt64();
                External = hs.ReadByteThrow();
                switch (External)
                {
                    case 0:
                        Folders = new Folder[NumFolders];
                        for (ulong i = 0; i < NumFolders; ++i)
                        {
                            Folders[i] = new Folder();
                            Folders[i].Parse(hs);
                        }
                        break;
                    case 1:
                        DataStreamsIndex = hs.ReadDecodedUInt64();
                        break;
                    default:
                        throw new SevenZipException("External value must be `0` or `1`.");
                }

                ExpectPropertyID(this, hs, PropertyID.kCodersUnPackSize);

                // CodersUnPackSize (data stored in `Folder.UnPackSizes`)

                for (ulong i = 0; i < NumFolders; ++i)
                {
                    Folders[i].UnPackSizes = new UInt64[Folders[i].NumOutStreamsTotal];
                    for (ulong j = 0; j < Folders[i].NumOutStreamsTotal; ++j)
                        Folders[i].UnPackSizes[j] = hs.ReadDecodedUInt64();
                }

                // Optional: UnPackDigests (data stored in `Folder.UnPackCRC`)

                PropertyID propertyID = GetPropertyID(this, hs);

                var UnPackDigests = new Digests(NumFolders);
                if (propertyID == PropertyID.kCRC)
                {
                    UnPackDigests.Parse(hs);
                    propertyID = GetPropertyID(this, hs);
                }
                for (ulong i = 0; i < NumFolders; ++i)
                    if (UnPackDigests.Defined(i))
                        Folders[i].UnPackCRC = UnPackDigests.CRCs[i];

                // end of UnPackInfo

                if (propertyID != PropertyID.kEnd)
                    throw new SevenZipException("Expected kEnd property.");
            }

            public void Write(Stream hs)
            {
                hs.WriteByte((Byte)PropertyID.kFolder);

                // Folders

                hs.WriteEncodedUInt64(NumFolders);
                hs.WriteByte(0);
                for (ulong i = 0; i < NumFolders; ++i)
                    Folders[i].Write(hs);

                // CodersUnPackSize in `Folder.UnPackSizes`

                hs.WriteByte((Byte)PropertyID.kCodersUnPackSize);
                for (ulong i = 0; i < NumFolders; ++i)
                    for (ulong j = 0; j < (ulong)Folders[i].UnPackSizes.LongLength; ++j)
                        hs.WriteEncodedUInt64(Folders[i].UnPackSizes[j]);
                
                // UnPackDigests in `Folder.UnPackCRC`

                if (Folders.Any(folder => folder.UnPackCRC != null))
                {
                    hs.WriteByte((Byte)PropertyID.kCRC);

                    var UnPackDigests = new Digests(NumFolders);
                    for (ulong i = 0; i < NumFolders; ++i)
                        UnPackDigests.CRCs[i] = Folders[i].UnPackCRC;
                    UnPackDigests.Write(hs);
                }

                hs.WriteByte((Byte)PropertyID.kEnd);
            }
        }

        internal class SubStreamsInfo : IHeaderParser, IHeaderWriter
        {
            UnPackInfo unPackInfo; // dependency

            public UInt64[] NumUnPackStreamsInFolders; // [NumFolders]
            public UInt64 NumUnPackStreamsTotal;
            public List<UInt64> UnPackSizes;
            public Digests Digests; // [Number of streams with unknown CRCs]
            public SubStreamsInfo(UnPackInfo unPackInfo)
            {
                this.unPackInfo = unPackInfo;
                this.NumUnPackStreamsInFolders = new UInt64[0];
                this.NumUnPackStreamsTotal = 0;
                this.UnPackSizes = new List<UInt64>();
                this.Digests = new Digests(0);
            }

            public void Parse(Stream hs)
            {
                PropertyID propertyID = GetPropertyID(this, hs);

                // Number of UnPack Streams per Folder

                if (propertyID == PropertyID.kNumUnPackStream)
                {
                    NumUnPackStreamsInFolders = new UInt64[unPackInfo.NumFolders];
                    NumUnPackStreamsTotal = 0;
                    for (ulong i = 0; i < unPackInfo.NumFolders; ++i)
                        NumUnPackStreamsTotal += NumUnPackStreamsInFolders[i] = hs.ReadDecodedUInt64();

                    propertyID = GetPropertyID(this, hs);
                }
                else // If no records, assume `1` output stream per folder
                {
                    NumUnPackStreamsInFolders = Enumerable.Repeat((UInt64)1, (int)unPackInfo.NumFolders).ToArray();
                    NumUnPackStreamsTotal = unPackInfo.NumFolders;
                }

                // UnPackSizes

                UnPackSizes = new List<UInt64>();
                if (propertyID == PropertyID.kSize)
                {
                    for (ulong i = 0; i < unPackInfo.NumFolders; ++i)
                    {
                        UInt64 num = NumUnPackStreamsInFolders[i];
                        if (num == 0)
                            continue;

                        UInt64 sum = 0;
                        for (ulong j = 1; j < num; ++j)
                        {
                            UInt64 size = hs.ReadDecodedUInt64();
                            sum += size;
                            UnPackSizes.Add(size);
                        }
                        UnPackSizes.Add(unPackInfo.Folders[i].GetUnPackSize() - sum);
                    }

                    propertyID = GetPropertyID(this, hs);
                }
                else // If no records, assume one unpack size per folder
                {
                    for (ulong i = 0; i < unPackInfo.NumFolders; ++i)
                    {
                        ulong num = NumUnPackStreamsInFolders[i];
                        if (num > 1)
                            throw new SevenZipException($"Invalid number of UnPackStreams `{num}` in Folder # `{i}`.");
                        if (num == 1)
                            UnPackSizes.Add(unPackInfo.Folders[i].GetUnPackSize());
                    }
                }

                // Digests [Number of Unknown CRCs]

                UInt64 numDigests = 0;
                for (UInt64 i = 0; i < unPackInfo.NumFolders; ++i)
                {
                    UInt64 numSubStreams = NumUnPackStreamsInFolders[i];
                    if (numSubStreams > 1 || unPackInfo.Folders[i].UnPackCRC == null)
                        numDigests += numSubStreams;
                }

                if (propertyID == PropertyID.kCRC)
                {
                    Digests = new Digests(numDigests);
                    Digests.Parse(hs);

                    propertyID = GetPropertyID(this, hs);
                }

                if (propertyID != PropertyID.kEnd)
                    throw new SevenZipException("Expected `kEnd` property ID.");
            }

            public void Write(Stream hs)
            {
                // Number of UnPacked Streams in Folders

                if (NumUnPackStreamsTotal != unPackInfo.NumFolders && NumUnPackStreamsInFolders.Any())
                {
                    hs.WriteByte((Byte)PropertyID.kNumUnPackStream);

                    for (long i = 0; i < NumUnPackStreamsInFolders.LongLength; ++i)
                        hs.WriteEncodedUInt64(NumUnPackStreamsInFolders[i]);
                }

                // UnPackSizes

                if (UnPackSizes.Any())
                {
                    hs.WriteByte((Byte)PropertyID.kSize);

                    List<UInt64>.Enumerator u = UnPackSizes.GetEnumerator();
                    for (long i = 0; i < NumUnPackStreamsInFolders.LongLength; ++i)
                    {
                        for (ulong j = 1; j < NumUnPackStreamsInFolders[i]; ++j)
                        {
                            if (!u.MoveNext())
                                throw new SevenZipException("Missing `SubStreamInfo.UnPackSize` entry.");
                            hs.WriteEncodedUInt64(u.Current);
                        }
                        u.MoveNext(); // skip the `unneeded` one
                    }
                }

                // Digests [Number of unknown CRCs]

                if (Digests.NumDefined() > 0)
                {
                    hs.WriteByte((Byte)PropertyID.kCRC);
                    Digests.Write(hs);
                }

                hs.WriteByte((Byte)PropertyID.kEnd);
            }
        }

        internal class StreamsInfo : IHeaderParser, IHeaderWriter
        {
            public PackInfo PackInfo;
            public UnPackInfo UnPackInfo;
            public SubStreamsInfo SubStreamsInfo;
            public StreamsInfo()
            {
                PackInfo = null;
                UnPackInfo = null;
                SubStreamsInfo = null;
            }

            public void Parse(Stream hs)
            {
                while (true)
                {
                    PropertyID propertyID = GetPropertyID(this, hs);
                    switch (propertyID)
                    {
                        case PropertyID.kPackInfo:
                            PackInfo = new PackInfo();
                            PackInfo.Parse(hs);
                            break;
                        case PropertyID.kUnPackInfo:
                            UnPackInfo = new UnPackInfo();
                            UnPackInfo.Parse(hs);
                            break;
                        case PropertyID.kSubStreamsInfo:
                            if (UnPackInfo == null)
                            {
                                Trace.TraceWarning("SubStreamsInfo block found, yet no UnPackInfo block has been parsed so far.");
                                UnPackInfo = new UnPackInfo();
                            }
                            SubStreamsInfo = new SubStreamsInfo(UnPackInfo);
                            SubStreamsInfo.Parse(hs);
                            break;
                        case PropertyID.kEnd:
                            return;
                        default:
                            throw new NotImplementedException(propertyID.ToString());
                    }
                }
            }

            public void Write(Stream hs)
            {
                if (PackInfo != null)
                {
                    hs.WriteByte((Byte)PropertyID.kPackInfo);
                    PackInfo.Write(hs);
                }
                if (UnPackInfo != null)
                {
                    hs.WriteByte((Byte)PropertyID.kUnPackInfo);
                    UnPackInfo.Write(hs);
                }
                if (SubStreamsInfo != null)
                {
                    hs.WriteByte((Byte)PropertyID.kSubStreamsInfo);
                    SubStreamsInfo.Write(hs);
                }
                hs.WriteByte((Byte)PropertyID.kEnd);
            }
        }

        internal abstract class FileProperty : IHeaderParser, IHeaderWriter
        {
            public PropertyID PropertyID;
            public UInt64 NumFiles;
            public UInt64 Size;
            public FileProperty(PropertyID PropertyID, UInt64 NumFiles)
            {
                this.PropertyID = PropertyID;
                this.NumFiles = NumFiles;
                Size = 0;
            }

            public virtual void Parse(Stream headerStream)
            {
                Size = headerStream.ReadDecodedUInt64();
                ParseProperty(headerStream);
            }
            public abstract void ParseProperty(Stream hs);

            public virtual void Write(Stream headerStream)
            {
                using (var dataStream = new MemoryStream())
                {
                    WriteProperty(dataStream);
                    Size = (UInt64)dataStream.Length;

                    headerStream.WriteByte((Byte)PropertyID);
                    headerStream.WriteEncodedUInt64(Size);
                    dataStream.Position = 0;
                    dataStream.CopyTo(headerStream);
                }
            }
            public abstract void WriteProperty(Stream hs);
        }

        internal class PropertyEmptyStream : FileProperty
        {
            public bool[] IsEmptyStream;
            public UInt64 NumEmptyStreams;
            public PropertyEmptyStream(UInt64 NumFiles) : base(PropertyID.kEmptyStream, NumFiles) { }

            public override void ParseProperty(Stream hs)
            {
                NumEmptyStreams = hs.ReadBoolVector(NumFiles, out IsEmptyStream);
            }

            public override void WriteProperty(Stream hs)
            {
                hs.WriteBoolVector(IsEmptyStream);
            }
        }

        internal class PropertyEmptyFile : FileProperty
        {
            public UInt64 NumEmptyStreams;
            public bool[] IsEmptyFile;
            public PropertyEmptyFile(UInt64 NumFiles, UInt64 NumEmptyStreams)
                : base(PropertyID.kEmptyFile, NumFiles)
            {
                this.NumEmptyStreams = NumEmptyStreams;
            }

            public override void ParseProperty(Stream hs)
            {
                hs.ReadBoolVector(NumEmptyStreams, out IsEmptyFile);
            }

            public override void WriteProperty(Stream hs)
            {
                hs.WriteBoolVector(IsEmptyFile);
            }
        }

        internal class PropertyAnti : FileProperty
        {
            public UInt64 NumEmptyStreams;
            public bool[] IsAnti;
            public PropertyAnti(UInt64 NumFiles, UInt64 NumEmptyStreams)
                : base(PropertyID.kAnti, NumFiles)
            {
                this.NumEmptyStreams = NumEmptyStreams;
            }

            public override void ParseProperty(Stream hs)
            {
                hs.ReadBoolVector(NumEmptyStreams, out IsAnti);
            }

            public override void WriteProperty(Stream hs)
            {
                hs.WriteBoolVector(IsAnti);
            }
        }

        internal class PropertyTime : FileProperty
        {
            public Byte External;
            public UInt64 DataIndex;
            public DateTime?[] Times; // [NumFiles]
            public PropertyTime(PropertyID propertyID, UInt64 NumFiles)
                : base(propertyID, NumFiles)
            {
            }

            public override void ParseProperty(Stream hs)
            {
                bool[] defined;
                var numDefined = hs.ReadOptionalBoolVector(NumFiles, out defined);

                External = hs.ReadByteThrow();
                switch (External)
                {
                    case 0:
                        Times = new DateTime?[NumFiles];
                        using (BinaryReader reader = new BinaryReader(hs, Encoding.Default, true))
                            for (ulong i = 0; i < NumFiles; ++i)
                            {
                                if (defined[i])
                                {
                                    UInt64 encodedTime = reader.ReadUInt64();
                                    if (encodedTime >= 0 && encodedTime <= 2650467743999999999)
                                        Times[i] = DateTime.FromFileTimeUtc((long)encodedTime).ToLocalTime();
                                    else
                                        Trace.TraceWarning($"Defined date # `{i}` is invalid.");
                                }
                                else
                                    Times[i] = null;
                            }
                        break;
                    case 1:
                        DataIndex = hs.ReadDecodedUInt64();
                        break;
                    default:
                        throw new SevenZipException("External value must be 0 or 1.");
                }
            }

            public override void WriteProperty(Stream hs)
            {
                bool[] defined = Times.Select(time => time != null).ToArray();
                hs.WriteOptionalBoolVector(defined);
                hs.WriteByte(0);
                using (BinaryWriter writer = new BinaryWriter(hs, Encoding.Default, true))
                    for (ulong i = 0; i < NumFiles; ++i)
                        if(Times[i] != null)
                        {
                            UInt64 encodedTime = (UInt64)(((DateTime)Times[i]).ToUniversalTime().ToFileTimeUtc());
                            writer.Write((UInt64)encodedTime);
                        }
            }
        }

        internal class PropertyName : FileProperty
        {
            public Byte External;
            public UInt64 DataIndex;
            public string[] Names;
            public PropertyName(UInt64 NumFiles) : base(PropertyID.kName, NumFiles) { }

            public override void ParseProperty(Stream hs)
            {
                External = hs.ReadByteThrow();
                if (External != 0)
                {
                    DataIndex = hs.ReadDecodedUInt64();
                }
                else
                {
                    Names = new string[NumFiles];
                    using (BinaryReader reader = new BinaryReader(hs, Encoding.Default, true))
                    {
                        List<Byte> nameData = new List<byte>(1024);
                        for (ulong i = 0; i < NumFiles; ++i)
                        {
                            nameData.Clear();
                            UInt16 ch;
                            while (true)
                            {
                                ch = reader.ReadUInt16();
                                if (ch == 0x0000)
                                    break;
                                nameData.Add((Byte)(ch >> 8));
                                nameData.Add((Byte)(ch & 0xFF));
                            }
                            Names[i] = Encoding.BigEndianUnicode.GetString(nameData.ToArray());
                        }
                    }
                }
            }

            public override void WriteProperty(Stream hs)
            {
                hs.WriteByte(0);
                using (BinaryWriter writer = new BinaryWriter(hs, Encoding.Default, true))
                {
                    for (ulong i = 0; i < NumFiles; ++i)
                    {
                        Byte[] nameData = Encoding.Unicode.GetBytes(Names[i]);
                        writer.Write(nameData);
                        writer.Write((UInt16)0x0000);
                    }
                }
            }
        }

        internal class PropertyAttributes : FileProperty
        {
            public Byte External;
            public UInt64 DataIndex;
            public UInt32?[] Attributes; // [NumFiles]
            public PropertyAttributes(UInt64 NumFiles) : base(PropertyID.kWinAttributes, NumFiles) { }

            public override void ParseProperty(Stream hs)
            {
                bool[] defined;
                var numDefined = hs.ReadOptionalBoolVector(NumFiles, out defined);

                External = hs.ReadByteThrow();
                switch (External)
                {
                    case 0:
                        Attributes = new UInt32?[NumFiles];
                        using (BinaryReader reader = new BinaryReader(hs, Encoding.Default, true))
                            for (ulong i = 0; i < NumFiles; ++i)
                                Attributes[i] = defined[i] ? (UInt32?)reader.ReadUInt32() : null;
                        break;
                    case 1:
                        DataIndex = hs.ReadDecodedUInt64();
                        break;
                    default:
                        throw new SevenZipException("External value must be 0 or 1.");
                }
            }

            public override void WriteProperty(Stream hs)
            {
                bool[] defined = Attributes.Select(attr => attr != null).ToArray();
                hs.WriteOptionalBoolVector(defined);
                hs.WriteByte(0);
                using (BinaryWriter writer = new BinaryWriter(hs, Encoding.Default, true))
                    for (ulong i = 0; i < NumFiles; ++i)
                        if (defined[i])
                            writer.Write((UInt32)Attributes[i]);
            }
        }

        internal class PropertyDummy : FileProperty
        {
            public PropertyDummy()
                : base(PropertyID.kDummy, 0) { }
            public override void ParseProperty(Stream hs)
            {
                Byte[] dummy = hs.ReadThrow(Size);
            }
            public override void WriteProperty(Stream hs)
            {
                hs.Write(Enumerable.Repeat((Byte)0, (int)Size).ToArray(), 0, (int)Size);
            }
        }

        internal class FilesInfo : IHeaderParser, IHeaderWriter
        {
            public UInt64 NumFiles;
            public UInt64 NumEmptyStreams;
            public List<FileProperty> Properties; // [Arbitrary number]
            public FilesInfo()
            {
                this.NumFiles = 0;
                this.NumEmptyStreams = 0;
                this.Properties = new List<FileProperty>();
            }

            public void Parse(Stream hs)
            {
                NumFiles = hs.ReadDecodedUInt64();
                while (true)
                {
                    PropertyID propertyID = GetPropertyID(this, hs);
                    if (propertyID == PropertyID.kEnd)
                        break;

                    FileProperty property = null;
                    switch (propertyID)
                    {
                        case PropertyID.kEmptyStream:
                            property = new PropertyEmptyStream(NumFiles);
                            property.Parse(hs);
                            NumEmptyStreams = (property as PropertyEmptyStream).NumEmptyStreams;
                            break;
                        case PropertyID.kEmptyFile:
                            property = new PropertyEmptyFile(NumFiles, NumEmptyStreams);
                            property.Parse(hs);
                            break;
                        case PropertyID.kAnti:
                            property = new PropertyAnti(NumFiles, NumEmptyStreams);
                            property.Parse(hs);
                            break;
                        case PropertyID.kCTime:
                        case PropertyID.kATime:
                        case PropertyID.kMTime:
                            property = new PropertyTime(propertyID, NumFiles);
                            property.Parse(hs);
                            break;
                        case PropertyID.kName:
                            property = new PropertyName(NumFiles);
                            property.Parse(hs);
                            break;
                        case PropertyID.kWinAttributes:
                            property = new PropertyAttributes(NumFiles);
                            property.Parse(hs);
                            break;
                        case PropertyID.kDummy:
                            property = new PropertyDummy();
                            property.Parse(hs);
                            break;
                        default:
                            throw new NotImplementedException(propertyID.ToString());
                    }

                    if (property != null)
                        Properties.Add(property);
                }
            }

            public void Write(Stream hs)
            {
                hs.WriteEncodedUInt64(NumFiles);
                foreach (var property in Properties)
                    property.Write(hs);
                hs.WriteByte((Byte)PropertyID.kEnd);
            }
        }

        internal class Header : IHeaderParser, IHeaderWriter
        {
            public ArchiveProperties ArchiveProperties;
            public StreamsInfo AdditionalStreamsInfo;
            public StreamsInfo MainStreamsInfo;
            public FilesInfo FilesInfo;
            public Header()
            {
                ArchiveProperties = null;
                AdditionalStreamsInfo = null;
                MainStreamsInfo = null;
                FilesInfo = null;
            }

            public void Parse(Stream hs)
            {
                while (true)
                {
                    PropertyID propertyID = GetPropertyID(this, hs);
                    switch (propertyID)
                    {
                        case PropertyID.kArchiveProperties:
                            ArchiveProperties = new ArchiveProperties();
                            ArchiveProperties.Parse(hs);
                            break;
                        case PropertyID.kAdditionalStreamsInfo:
                            AdditionalStreamsInfo = new StreamsInfo();
                            AdditionalStreamsInfo.Parse(hs);
                            break;
                        case PropertyID.kMainStreamsInfo:
                            MainStreamsInfo = new StreamsInfo();
                            MainStreamsInfo.Parse(hs);
                            break;
                        case PropertyID.kFilesInfo:
                            FilesInfo = new FilesInfo();
                            FilesInfo.Parse(hs);
                            break;
                        case PropertyID.kEnd:
                            return;
                        default:
                            throw new NotImplementedException(propertyID.ToString());
                    }
                }
            }

            public void Write(Stream hs)
            {
                if (ArchiveProperties != null)
                {
                    hs.WriteByte((Byte)PropertyID.kArchiveProperties);
                    ArchiveProperties.Write(hs);
                }
                if (AdditionalStreamsInfo != null)
                {
                    hs.WriteByte((Byte)PropertyID.kAdditionalStreamsInfo);
                    AdditionalStreamsInfo.Write(hs);
                }
                if (MainStreamsInfo != null)
                {
                    hs.WriteByte((Byte)PropertyID.kMainStreamsInfo);
                    MainStreamsInfo.Write(hs);
                }
                if (FilesInfo != null)
                {
                    hs.WriteByte((Byte)PropertyID.kFilesInfo);
                    FilesInfo.Write(hs);
                }
                hs.WriteByte((Byte)PropertyID.kEnd);
            }
        }
        #endregion Internal Classes

        #region Internal Properties
        internal Header RawHeader
        {
            get; set;
        }
        internal StreamsInfo EncodedHeader
        {
            get; set;
        }
        #endregion Internal Properties

        #region Private Fields
        Stream headerStream;
        #endregion Private Fields

        #region Internal Constructors
        /// <summary>
        /// 7zip file header constructor
        /// </summary>
        internal SevenZipHeader(Stream headerStream, bool createNew = false)
        {
            this.headerStream = headerStream;
            RawHeader = createNew ? new Header() : null;
            EncodedHeader = null;
        }
        #endregion Internal Constructors

        #region Public Methods (Interfaces)
        /// <summary>
        /// Main parser entry point.
        /// </summary>
        public void Parse(Stream headerStream)
        {
            try
            {
                var propertyID = GetPropertyID(this, headerStream);
                switch (propertyID)
                {
                    case PropertyID.kHeader:
                        RawHeader = new Header();
                        RawHeader.Parse(headerStream);
                        break;

                    case PropertyID.kEncodedHeader:
                        EncodedHeader = new StreamsInfo();
                        EncodedHeader.Parse(headerStream);
                        break;

                    case PropertyID.kEnd:
                        return;

                    default:
                        throw new NotImplementedException(propertyID.ToString());
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning(ex.GetType().Name + ": " + ex.Message + Environment.NewLine + ex.StackTrace);
            }
        }

        /// <summary>
        /// Main writer that initiates header writing
        /// </summary>
        public void Write(Stream headerStream)
        {
            try
            {
                if (RawHeader != null)
                {
                    headerStream.WriteByte((Byte)PropertyID.kHeader);
                    RawHeader.Write(headerStream);
                }
                else if (EncodedHeader != null)
                {
                    headerStream.WriteByte((Byte)PropertyID.kEncodedHeader);
                    EncodedHeader.Write(headerStream);
                }
                else
                    throw new SevenZipException("No header to write.");
            }
            catch (Exception ex)
            {
                Trace.TraceWarning(ex.GetType().Name + ": " + ex.Message + Environment.NewLine + ex.StackTrace);
            }
        }
        #endregion Public Methods (Interfaces)

        #region Internal Methods
        /// <summary>
        /// Main parser that initiates cascaded parsing
        /// </summary>
        internal void Parse()
        {
            Parse(headerStream);
        }

        /// <summary>
        /// Helper function to return a property id while making sure it's valid (+ trace)
        /// </summary>
        internal static PropertyID GetPropertyID(IHeaderParser parser, Stream headerStream)
        {
            Byte propertyID = headerStream.ReadByteThrow();
            if (propertyID > (Byte)PropertyID.kDummy)
                throw new SevenZipException(parser.GetType().Name + $": Unknown property ID = {propertyID}.");

            Trace.TraceInformation(parser.GetType().Name + $": Property ID = {(PropertyID)propertyID}");
            return (PropertyID)propertyID;
        }

        /// <summary>
        /// Helper function to read and ensure a specific PropertyID is next in header stream
        /// </summary>
        internal static void ExpectPropertyID(IHeaderParser parser, Stream headerStream, PropertyID propertyID)
        {
            if (GetPropertyID(parser, headerStream) != propertyID)
                throw new SevenZipException(parser.GetType().Name + $": Expected property ID = {propertyID}.");
        }
        #endregion Internal Methods
    }
}
