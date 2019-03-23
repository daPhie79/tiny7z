using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace pdj.tiny7z
{
    class Program
    {
        public static readonly string InternalDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

        public static Version AppVersion
        {
            get => Assembly.GetExecutingAssembly().GetName().Version;
        }

        public enum ParseState
        {
            Command,
            ArchiveFileName,
            FileNames
        }

        public enum ArchiveAction
        {
            None,
            Add,
            Extract,
            ExtractFull
        }

        static ArchiveAction archiveAction = ArchiveAction.None;
        static bool overwrite = false;
        static string archiveFileName = string.Empty;
        static string outputPath = string.Empty;
        static List<string> fileNames = new List<string>();
        static DateTime timer;

        static void Main(string[] args)
        {
            // set logger

            Console.OutputEncoding = Encoding.Unicode;
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();

            System.Diagnostics.Trace.Listeners.Add(new SerilogTraceListener.SerilogTraceListener());

            Log.Logger.Information("tiny7zTool v{Version} Starting.", AppVersion);

            bool proceed = ProcessCommandLine(args);
            if (!proceed)
            {
                PrintHelp();
            }
            else
            {
                switch (archiveAction)
                {
                    case ArchiveAction.Add:
                        CompressFiles();
                        break;
                    case ArchiveAction.Extract:
                        ExtractFiles(false);
                        break;
                    case ArchiveAction.ExtractFull:
                        ExtractFiles(true);
                        break;
                }
            }
            Log.Information("All done!");
        }

        static bool ProgressEvent(Archive.IProgressProvider provider, bool included, int currentFileIndex, ulong currentFileSize, ulong filesSize, ulong rawSize, ulong compressedSize)
        {
            if (currentFileIndex >= provider.Files.Count)
            {
                string status = (archiveAction == ArchiveAction.Add ? "Compressing" : "Extracting") + ": 100% Done!";
                status = status + new string(' ', Console.BufferWidth - 1 - status.Length);
                Console.Write(status);
                Console.SetCursorPosition(0, Console.CursorTop);
            }
            else if (timer == default(DateTime) || DateTime.Now.Subtract(timer).Milliseconds > 250)
            {
                timer = DateTime.Now;
                string status;
                if (included)
                {
                    status = string.Format("File: {0}, {1}%", provider.Files[currentFileIndex].Name, rawSize * 100 / provider.RawTotalSize);
                }
                else
                {
                    status = string.Format("Skipping: {0}%", rawSize * 100 / provider.RawTotalSize);
                }

                // formatting
                if (status.Length >= Console.BufferWidth)
                {
                    status = status.Substring(0, Console.BufferWidth - 1);
                }
                else
                {
                    status = status + new string(' ', Console.BufferWidth - 1 - status.Length);
                }
                Console.Write(status);
                Console.SetCursorPosition(0, Console.CursorTop);
            }
            return true;
        }

        static void CompressFiles()
        {
            if (!fileNames.Any())
            {
                fileNames.Add(Directory.GetCurrentDirectory());
            }

            using (var archive = new Archive.SevenZipArchive(File.Create(archiveFileName), FileAccess.Write))
            {
                var compressor = archive.Compressor();
                compressor.CompressHeader = true;
                compressor.PreserveDirectoryStructure = true;
                compressor.Solid = true;

                foreach (var fn in fileNames)
                {
                    if (fn.IndexOfAny(new[] { '?', '*' }) != -1)
                    {
                        string path = Path.Combine(Directory.GetCurrentDirectory(), Path.GetDirectoryName(fn));
                        string pattern = Path.GetFileName(fn);

                        foreach (var file in Directory.EnumerateFiles(path, pattern, SearchOption.TopDirectoryOnly))
                        {
                            Log.Information("Compressing {File}...", Path.GetFileName(file));
                            compressor.AddFile(file);
                        }
                    }
                    else
                    {
                        if (File.Exists(fn))
                        {
                            Log.Information("Compressing {File}...", Path.GetFileName(fn));
                            compressor.AddFile(fn);
                        }
                        else if (Directory.Exists(fn))
                        {
                            var info = new DirectoryInfo(fn);
                            Log.Information("Compressing contents of {Directory}...", Path.GetFileName(info.Name));
                            compressor.AddDirectory(info.FullName);
                        }
                    }
                }

                var timer = DateTime.Now;
                compressor.ProgressDelegate = ProgressEvent;

                var now = DateTime.Now;
                compressor.Finalize();
                var ela = DateTime.Now.Subtract(now);

                Console.WriteLine();
                Log.Information("Compression took {ela}ms.", ela.TotalMilliseconds);
            }
        }

        static void ExtractFiles(bool preserveDirectoryStructure)
        {
            using (var archive = new Archive.SevenZipArchive(File.OpenRead(archiveFileName), FileAccess.Read))
            {
                var extractor = archive.Extractor();
                extractor.OverwriteExistingFiles = false;
                extractor.PreserveDirectoryStructure = preserveDirectoryStructure;
                extractor.SkipExistingFiles = true;

                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    outputPath = Directory.GetCurrentDirectory();
                }

                var timer = DateTime.Now;
                extractor.ProgressDelegate = ProgressEvent;

                var now = DateTime.Now;
                if (!fileNames.Any())
                {
                    Log.Information("Extracting files into \"{Path}\"...", Path.GetFileName(outputPath));
                    extractor.ExtractArchive(outputPath);
                }
                else
                {
                    try
                    {
                        Log.Information("Extracting file(s) \"{FileNames}\" into \"{Path}\"...", string.Join(", ", fileNames), Path.GetFileName(outputPath));
                        extractor.ExtractFiles(fileNames.ToArray(), outputPath);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "There was an error attempting to extract file.");
                    }
                }
                var ela = DateTime.Now.Subtract(now);

                Console.WriteLine();
                Log.Information("Decompression took {ela}ms.", ela.TotalMilliseconds);
            }
        }

        static void PrintHelp()
        {
            Log.Information("Usage: t7zt.exe <command> [options] <archive file> [<filenames>]");
            Log.Information("  <command> :");
            Log.Information("    a add files to archive");
            Log.Information("    e extract files from archive (ignoring paths)");
            Log.Information("    x extract files from archive (full paths)");
            Log.Information("  [options] :");
            Log.Information("    -o \"output path\" : specify output directory for extracted files.");
            Log.Information("  <archive file> : name of .7z archive file to compress to, or decompress from");
            Log.Information("  <filenames> : space separated list of files to add to or extract from archive");
            Console.WriteLine();
        }

        static bool ProcessCommandLine(string[] args)
        {
            ParseState state = ParseState.Command;
            bool error = false;
            for (int i = 0; i < args.Length && !error; i++)
            {
                string arg = args[i];
                if (arg.StartsWith("-"))
                {
                    switch (arg.ToLowerInvariant())
                    {
                        case "-o":
                            if (++i == args.Length)
                            {
                                Log.Error("Invalid -o parameter. Missing output path.");
                                error = true;
                            }
                            else
                            {
                                outputPath = args[i];
                                if (!Directory.Exists(outputPath))
                                {
                                    Log.Information("Creating output path: {OutputPath}", outputPath);
                                    Directory.CreateDirectory(outputPath);
                                }
                                else
                                {
                                    Log.Information("Using \"{OutputPath}\" as output path.", outputPath);
                                }
                            }
                            break;
                        case "-x":
                            Log.Information("-x Overwrite output archive if it already exists.");
                            overwrite = true;
                            break;
                        default:
                            Log.Error("Invalid parameter: {Arg}", arg);
                            error = true;
                            break;
                    }
                }
                else
                {
                    switch (state)
                    {
                        case ParseState.Command:
                            if (arg.Length == 1)
                            {
                                switch (arg.ToLower()[0])
                                {
                                    case 'a':
                                        Log.Information("a  Adding files to archive.");
                                        archiveAction = ArchiveAction.Add;
                                        break;
                                    case 'e':
                                        Log.Information("e  Extracting files from archive.");
                                        archiveAction = ArchiveAction.Extract;
                                        break;
                                    case 'x':
                                        Log.Information("x  Extracting files from archive (full path).");
                                        archiveAction = ArchiveAction.ExtractFull;
                                        break;
                                    default:
                                        Log.Error("Invalid command: {Arg}", arg);
                                        error = true;
                                        break;
                                }
                                state = ParseState.ArchiveFileName;
                            }
                            else
                            {
                                Log.Error("Invalid command: {Arg}", arg);
                                error = true;
                            }
                            break;
                        case ParseState.ArchiveFileName:
                            archiveFileName = arg;
                            if (!archiveFileName.ToLowerInvariant().EndsWith(".7z"))
                            {
                                archiveFileName += ".7z";
                            }
                            Log.Information("Using archive filename: {FileName}", archiveFileName);
                            state = ParseState.FileNames;
                            break;
                        case ParseState.FileNames:
                            Log.Information("Including \"{Arg}\" in file list.", arg);
                            fileNames.Add(arg);
                            break;
                    }
                }
            }

            if (!error)
            {
                if (archiveAction == ArchiveAction.None)
                {
                    Log.Error("No action requested.");
                    error = true;
                }
                else if (string.IsNullOrWhiteSpace(archiveFileName))
                {
                    Log.Error("No archive file specified.");
                    error = true;
                }
                else if (archiveAction == ArchiveAction.Add && !overwrite && File.Exists(archiveFileName))
                {
                    Log.Error("Archive filename \"{FileName}\" already exists. Cannot overwrite.", archiveFileName);
                    error = true;
                }
            }
            
            Console.WriteLine("");
            return !error;
        }
    }
}
