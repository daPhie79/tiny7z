using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SevenZip.Compression.LZMA
{
    public class EncoderProperties
    {
        internal CoderPropID[] propIDs;
        internal object[] properties;

        public EncoderProperties()
            : this(false)
        {
        }

        public EncoderProperties(bool eos)
            : this(eos, 1 << 24)
        {
        }

        public EncoderProperties(bool eos, int dictionary)
            : this(eos, dictionary, 128)
        {
        }

        public EncoderProperties(bool eos, int dictionary, int numFastBytes)
        {
            int posStateBits = 2;
            int litContextBits = 3;
            int litPosBits = 0;
            int algorithm = 2;
            string mf = "bt4";

            propIDs = new[]
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
            properties = new object[]
                         {
                             dictionary,
                             posStateBits,
                             litContextBits,
                             litPosBits,
                             algorithm,
                             numFastBytes,
                             mf,
                             eos
                         };
        }
    }
}
