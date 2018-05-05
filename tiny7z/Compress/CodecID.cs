using System;
using System.Linq;

namespace pdj.tiny7z.Compress
{
    /// <summary>
    /// CodecID class to help manage codec ids (array of bytes) as equatable entities
    /// </summary>
    public class CodecID : IEquatable<CodecID>
    {
        byte[] id;

        public CodecID() : this(new byte[0]) { }
        public CodecID(params byte[] id)
        {
            this.id = id.ToArray();
        }

        public int Size
        {
            get => id.Length;
        }

        public byte[] Raw
        {
            get => id;
        }

        public static bool operator ==(CodecID c1, CodecID c2)
        {
            if (ReferenceEquals(c1, null) && ReferenceEquals(c2, null))
                return true;
            if (ReferenceEquals(c1, null) || ReferenceEquals(c2, null))
                return false;
            return c1.Equals(c2);
        }

        public static bool operator !=(CodecID c1, CodecID c2)
        {
            return !(c1 == c2);
        }

        public override bool Equals(object obj)
        {
            return !ReferenceEquals(obj, null) && Equals(obj as CodecID);
        }

        public bool Equals(CodecID otherCodecID)
        {
            if (otherCodecID.id.Length != id.Length)
            {
                return false;
            }
            return id.SequenceEqual(otherCodecID.id);
        }

        public override int GetHashCode()
        {
            return ComputeHash(id);
        }

        private static int ComputeHash(params byte[] data)
        {
            unchecked
            {
                const int p = 16777619;
                int hash = (int)2166136261;

                for (int i = 0; i < data.Length; i++)
                    hash = (hash ^ data[i]) * p;

                hash += hash << 13;
                hash ^= hash >> 7;
                hash += hash << 3;
                hash ^= hash >> 17;
                hash += hash << 5;
                return hash;
            }
        }
    }
}
