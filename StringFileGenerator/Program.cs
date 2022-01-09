using StringFileGenerator.Resources;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace StringFileGenerator
{
    class Program
    {
        static StringFileGenerator stringFileGenerator;

        /// <summary>
        /// Maximum value of memory used by this process
        /// </summary>
        static long maxMemoryUsed = 0;

        static async Task<int> Main(string[] args)
        {
            int exitCode = 1;
            Task memUsageTask = null;
            Stopwatch stopWatch = new Stopwatch();
            bool GracefulCancel = false;
            var cts = new CancellationTokenSource();

            string outputFilename;
            int targetSize;
            int repetitionThreshold;

            Console.CancelKeyPress += (s, e) =>
            {
                if (!GracefulCancel)
                    Environment.Exit(1);
                Console.WriteLine("Canceling...");
                cts.Cancel();
                e.Cancel = true;
                GracefulCancel = false;
            };
            try
            {
                // Preparing variables
                memUsageTask = CountMaxMem(cts.Token);
                if (args.Length < 1
                    || Array.Exists(new string[4] { "/?", "/h", "-help", "--help" }, input => args[0] == input))
                {
                    Console.WriteLine(Constants.helpInfo);
                    return exitCode;
                }

                if (!(args.Length > 1 && int.TryParse(args[1], out targetSize) && targetSize > 0 && targetSize < 100))
                {
                    targetSize = 100;
                }

                if (!(args.Length > 2 && int.TryParse(args[2], out repetitionThreshold) && repetitionThreshold > 0 && repetitionThreshold < 100))
                {
                    repetitionThreshold = 10;
                }

                outputFilename = args[0];

                if (outputFilename.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    throw new DetailedMessageException("Invalid output file name", null);
                }

                if (File.Exists(outputFilename))
                {
                    Console.WriteLine($"The output file already exists. Do you want to overwrite it? (y/n)");
                    var i = Console.ReadKey();
                    Console.WriteLine(string.Empty);
                    if (i.Key == ConsoleKey.Y)
                    {
                        try
                        {
                            File.Delete(outputFilename);
                        }
                        catch (IOException ex)
                        {
                            throw new DetailedMessageException("Error deleting output file. The output file is in use.", ex);
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            throw new DetailedMessageException("Error deleting output file. The program does not have the required permission.", ex);
                        }
                        catch (Exception ex)
                        {
                            throw new DetailedMessageException("Error deleting output file.", ex);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Operation aborted.");
                        return exitCode;
                    }
                }

                stopWatch.Start();

                GracefulCancel = true;

                // Main execution
                stringFileGenerator = new StringFileGenerator(outputFilename, (long)1024 * 1024 * targetSize, repetitionThreshold);
                Console.WriteLine($"Generating file \"{outputFilename}\" with size {targetSize:n0}MB and repetition threshold {repetitionThreshold}%");
                await stringFileGenerator.GenerateAsync();

                exitCode = 0;
            }
            catch (DetailedMessageException dme)
            {
                Console.WriteLine($"Error: {dme.Message}\r\nDetails:");
                if (dme.InnerException != null) 
                    Console.WriteLine(dme.InnerException);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Operation cancelled.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled exception. Please send a content of this screen to the developer: artishev.ds@gmail.com");
                Console.WriteLine(ex);
            }
            finally
            {
                cts.Cancel();
                stopWatch.Stop();
            }

            TimeSpan ts = stopWatch.Elapsed;
            Console.WriteLine($"Operation RunTime {ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}:{ts.Milliseconds / 10:00}");

            return 0;
        }

        /// <summary>
        /// Determinates maximum memory usage
        /// </summary>
        /// <param name="ct">Cancelation token. Must be cancelled to stop memory analyze</param>
        /// <returns>Task represents process of memory analyze</returns>
        static private async Task CountMaxMem(CancellationToken ct)
        {
            do
            {
                long usedMemory = Process.GetCurrentProcess().PrivateMemorySize64;
                if (maxMemoryUsed < usedMemory)
                {
                    maxMemoryUsed = usedMemory;
                }

                await Task.Delay(100);
            } while (!ct.IsCancellationRequested);
        }
    }
}
