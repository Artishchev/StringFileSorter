using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace StringFileGenerator
{
    class Program
    {
        static StringFileGenerator stringFileGenerator;
        static async Task<int> Main(string[] args)
        {
            string outputFilename = "output.txt";
            long targetSize = 1024 * 1024 * 1000;

            Stopwatch stopWatch = new Stopwatch();

            if (File.Exists(outputFilename))
                File.Delete(outputFilename);

            stopWatch.Start();

            stringFileGenerator = new StringFileGenerator(outputFilename, targetSize);
            Console.WriteLine($"Generating file \"{outputFilename}\" with size {targetSize:n0}");
            await stringFileGenerator.Generate();

            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            Console.WriteLine($"Operation RunTime {ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}:{ts.Milliseconds / 10:00}");

            return 0;
        }
    }
}
