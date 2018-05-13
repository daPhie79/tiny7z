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
