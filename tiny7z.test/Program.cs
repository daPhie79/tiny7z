using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace pdj.tiny7z
{
    class Program
    {
        public static readonly string InternalBase = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        [STAThread]
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

            // try compression

            new Compress.CodecLZMA();
            try
            {
                string destFileName = Path.Combine(InternalBase, "OutputTest.7z");
                z7Archive f = new z7Archive(File.Create(destFileName), FileAccess.Write);
                var cmp = f.Compressor();
                (cmp as z7Compressor).Solid = true;
                (cmp as z7Compressor).CompressHeader = true;

                FolderBrowserDialog fbd = new FolderBrowserDialog();
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    if (!Directory.Exists(fbd.SelectedPath))
                        throw new ApplicationException("Path not found.");
                    cmp.CompressAll(fbd.SelectedPath, true);
                    f.Dump();
                    f.Close();

                    // try decompression

                    string sourceFileName = Path.Combine(InternalBase, "OutputTest.7z");
                    z7Archive f2 = new z7Archive(File.OpenRead(sourceFileName), FileAccess.Read);
                    var ext = f2.Extractor();
                    f2.Dump();
                }
                else
                {
                    Trace.TraceWarning("Cancelling...");
                }

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
