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
        private string filename;
        private long targetSize;
        private int repetitionThreshold;
        private long currentSize = 0;
        private object lockObject = new object();

        public StringFileGenerator(string filename, long targetSize, int repetitionThreshold)
        {
            this.filename = filename;
            this.targetSize = targetSize;
            this.repetitionThreshold = repetitionThreshold;
        }

        public async Task Generate()
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

                await WriteToFile(buffer);
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

        private async Task WriteToFile(byte[] outputData)
        {
            using Stream stream = File.Open(filename, FileMode.Append);
            {
                await stream.WriteAsync(outputData);
            }
        }

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
