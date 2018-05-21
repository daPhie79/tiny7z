using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pdj.tiny7z.Compress
{
    public class CodecLZMA : Codec
    {
        public CodecLZMA() : base(new byte[] { 0x03, 0x01, 0x01 })
        {
            SetDefaultEncoderOptions();
            SetDefaultDecoderOptions();
        }

        public override ICoder GetCompressor()
        {
            var coder = new SevenZip.Compression.LZMA.Encoder();
            if (coderProperties.Any())
            {
                coder.SetCoderProperties(coderProperties.Keys.ToArray(), coderProperties.Values.ToArray());
            }
            return coder;
        }

        public override ICoder GetDecompressor()
        {
            var decoder = new SevenZip.Compression.LZMA.Decoder();
            if (decoderProperties != null)
                decoder.SetDecoderProperties(decoderProperties);
            return decoder;
        }

        void SetDefaultEncoderOptions()
        {
            Int32 dictionary = 1 << 23;
            //Int32 dictionary = 1 << 21;
            string mf = "bt4";
            Int32 posStateBits = 2;
            Int32 litContextBits = 3; // for normal files
            //UInt32 litContextBits = 0; // for 32-bit data
            Int32 litPosBits = 0;
            //UInt32 litPosBits = 2; // for 32-bit data
            Int32 algorithm = 2;
            Int32 numFastBytes = 128;
            bool eos = false;

            CoderPropID[] propIDs =
            {
                CoderPropID.DictionarySize,
                CoderPropID.PosStateBits,
                CoderPropID.LitContextBits,
                CoderPropID.LitPosBits,
                CoderPropID.Algorithm,
                CoderPropID.NumFastBytes,
                CoderPropID.MatchFinder,
                CoderPropID.EndMarker
            };
            object[] properties =
            {
                (Int32)(dictionary),
                (Int32)(posStateBits),
                (Int32)(litContextBits),
                (Int32)(litPosBits),
                (Int32)(algorithm),
                (Int32)(numFastBytes),
                mf,
                eos
            };
            SetCoderProperties(propIDs, properties);
        }

        void SetDefaultDecoderOptions()
        {
        }
    }
}
