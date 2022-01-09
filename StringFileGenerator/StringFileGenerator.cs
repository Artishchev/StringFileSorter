using StringFileGenerator.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace StringFileGenerator
{
    internal class StringFileGenerator
    {
        /// <summary>
        /// Output file name
        /// </summary>
        private string filename;

        /// <summary>
        /// Desired size of the output file
        /// </summary>
        private long targetSize;

        /// <summary>
        /// Repeated string percentage
        /// </summary>
        private int repetitionThreshold;

        /// <summary>
        /// Current size of a generated data
        /// </summary>
        private long currentSize = 0;

        /// <summary>
        /// Thread synchronization object for output size managment
        /// </summary>
        private object lockObject = new object();

        /// <summary>
        /// Generates file wit random strings
        /// </summary>
        /// <param name="filename">Output file name</param>
        /// <param name="targetSize">Output file size</param>
        /// <param name="repetitionThreshold">Repeated string percentage</param>
        public StringFileGenerator(string filename, long targetSize, int repetitionThreshold)
        {
            this.filename = filename;
            this.targetSize = targetSize;
            this.repetitionThreshold = repetitionThreshold;
        }

        /// <summary>
        /// Generates output file
        /// </summary>
        /// <returns>AsyncTask</returns>
        public async Task GenerateAsync()
        {
            var agregateBlock = new BatchBlock<byte[]>(1000*2);

            var writeBlock = new ActionBlock<IEnumerable<byte[]>>(async (IEnumerable<byte[]> inputData) => {
                long bufferSize = 0;
                foreach (var item in inputData)
                {
                    bufferSize += item.Length;
                }

                byte[] buffer = new byte[bufferSize];
                long index = 0;
                foreach (var item in inputData)
                {
                    item.CopyTo(buffer, index);
                    index += item.Length;
                }

                await WriteToFileAsync(buffer);
            });

            agregateBlock.LinkTo(writeBlock, new DataflowLinkOptions() { PropagateCompletion = true });

            List<Task> generatorTasks = new List<Task>();
            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                generatorTasks.Add(Task.Run(async ()=> {
                    // Main random generator
                    Random rnd = new Random();
                    while (true)
                    {
                        // Resulting string
                        StringBuilder result = new StringBuilder();

                        // String to be repeated
                        StringBuilder stringToRepead = new StringBuilder();

                        for (int i = 0; i < 1000; i++)
                        {
                            // generating number
                            result.Append(rnd.Next(1, 100000));
                            result.Append(". ");

                            if (stringToRepead.Length > 0 && rnd.Next(1, 100) < repetitionThreshold)
                            {
                                // Repeating
                                result.Append(stringToRepead);
                                if (rnd.Next(0, 2) > 0) // Repeat more times
                                    stringToRepead.Clear();
                            }
                            else
                            {
                                // New string
                                int lengthBeforeStringAdded = result.Length;
                                GetRandomString(rnd.Next(1, 5), rnd, result);

                                if (stringToRepead.Length == 0)
                                {
                                    stringToRepead.Append(result, lengthBeforeStringAdded, result.Length - lengthBeforeStringAdded);
                                }
                            }
                            
                            result.Append(Environment.NewLine);
                        }

                        byte[] buffer = Encoding.UTF8.GetBytes(result.ToString());
                        if (TryIncrementSize(buffer.Length))
                        {
                            await agregateBlock.SendAsync(buffer);
                        }
                        else
                        {
                            break;
                        }
                    }
                }));
            }

            await Task.WhenAll(generatorTasks);

            agregateBlock.Complete();

            await writeBlock.Completion;
        }

        /// <summary>
        /// Writes buffered data to the output file
        /// </summary>
        /// <param name="outputData">Buffered data</param>
        /// <returns>Async task</returns>
        private async Task WriteToFileAsync(byte[] outputData)
        {
            using Stream stream = File.Open(filename, FileMode.Append);
            {
                await stream.WriteAsync(outputData);
            }
        }

        /// <summary>
        /// Increments current output data size
        /// </summary>
        /// <param name="size">Size to be added</param>
        /// <returns>True if amount is acceptable. False if it more than desired</returns>
        private bool TryIncrementSize(int size)
        {
            bool result = false;
            lock (lockObject)
            {
                if (currentSize + size < targetSize)
                {
                    currentSize += size;
                    result = true;
                }
            }
            return result;
        }
        
        /// <summary>
        /// Generates random string from the dictionary
        /// </summary>
        /// <param name="count">Words in new string</param>
        /// <param name="rnd">Random generator</param>
        /// <param name="result">Resulting string where new words will be added</param>
        private void GetRandomString(int count, Random rnd, StringBuilder result)
        {
            string[] words = new string[count];

            for (int i = 0; i < count; i++)
            {
                words[i] = Constants.wordList[rnd.Next(Constants.wordList.Length - 1)];
            }
            result.AppendJoin(' ', words);
        }

    }
}
