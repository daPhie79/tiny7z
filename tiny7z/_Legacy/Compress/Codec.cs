using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pdj.tiny7z.Compress
{
    public abstract class Codec : ISetCoderProperties, ISetDecoderProperties
    {
        /// <summary>
        /// Static list elements
        /// </summary>
        static Dictionary<CodecID, Codec> registeredCodecs = new Dictionary<CodecID, Codec>();
        protected static void registerCodec(Codec codec)
        {
            registeredCodecs.Add(codec.ID, codec);
        }
        public static Codec Query(CodecID codecID)
        {
            if (registeredCodecs.ContainsKey(codecID))
                return registeredCodecs[codecID];
            return null;
        }

        /// <summary>
        /// Protected elements to be used by classes that implements this
        /// </summary>
        protected Dictionary<CoderPropID, object> coderProperties = null;
        protected byte[] decoderProperties = null;

        /// <summary>
        /// Public properties
        /// </summary>
        public CodecID ID
        {
            get; private set;
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public Codec(byte[] ID)
        {
            this.ID = new CodecID(ID);
            registerCodec(this);
        }

        /// <summary>
        /// Those have to be overridden
        /// </summary>
        public abstract ICoder GetCompressor();
        public abstract ICoder GetDecompressor();

        /// <summary>
        /// Optionally can ben overriden, but default implementation is already there
        /// </summary>
        public virtual void SetCoderProperties(CoderPropID[] propIDs, object[] properties)
        {
            if (properties.Length < propIDs.Length)
                throw new ArgumentOutOfRangeException();

            if (coderProperties == null)
                coderProperties = new Dictionary<CoderPropID, object>();
            for (int i = 0; i < propIDs.Length; ++i)
            {
                coderProperties[propIDs[i]] = properties[i];
            }
        }
        public virtual void SetDecoderProperties(byte[] properties)
        {
            decoderProperties = properties.ToArray();
        }
    }
}
