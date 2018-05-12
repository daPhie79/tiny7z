using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace pdj.tiny7z.Common
{
    public static class Util
    {
        public static void Dump(object o)
        {
            string json = JsonConvert.SerializeObject(o, Formatting.Indented);
            System.Diagnostics.Trace.WriteLine(json);
        }
    }
}
