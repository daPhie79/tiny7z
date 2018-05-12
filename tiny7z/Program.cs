using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace pdj.tiny7z
{
    class Program
    {
        public static readonly string InternalBase = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        static void Main(string[] args)
        {
            try
            {
                File.Delete(Path.Combine(Program.InternalBase, "debuglog.txt"));
            }
            catch { }

            var ctl = new ConsoleTraceListener();
            Trace.Listeners.Add(ctl);
            Trace.Listeners.Add(new TextWriterTraceListener(
                Path.Combine(Program.InternalBase, "debuglog.txt")));
            Trace.AutoFlush = true;

            new Compress.CodecLZMA();
            try
            {
                string destFileName = Path.Combine(InternalBase, "OutputTest.7z");
                z7Archive f = new z7Archive(File.Create(destFileName), FileAccess.Write);
                var cmp = f.Compressor();
                (cmp as z7Compressor).Solid = true;
                cmp.CompressAll(@"D:\ALLROMS\My Selection\PCE", true);
                f.Dump();
                f.Close();
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.Message + ex.StackTrace);
            }

            try {
                string sourceFileName = Path.Combine(InternalBase, "OutputTest.7z");
                z7Archive f2 = new z7Archive(File.OpenRead(sourceFileName), FileAccess.Read);
                var ext = f2.Extractor();
                f2.Dump();
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.Message + ex.StackTrace);
            }

            Console.WriteLine("Press any key to end...");
            Console.ReadKey();
        }
    }
}
