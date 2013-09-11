using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Icklewik.Core.File
{
    /// <summary>
    /// Specifies how a file reader should behave if it catches an IOException (which generally
    /// signifies that a file handle is still in use, something that can happen quite a lot
    /// when we are watching the file system)
    /// </summary>
    public enum FileReaderPolicy
    {
        Immediate,          // file reader will rethrow IOExceptions 
        LimitedBlock,       // will attempt to read again for a configurable amount of time, then retrhow IOException
        IndefiniteBlock     // will attempt to read again forever
    }

    public class FileReader
    {
        private FileReaderPolicy policy;
        private int timeoutMilliseconds = 0;

        public FileReader(FileReaderPolicy policy, int timeoutMilliseconds = 0)
        {
            this.policy = policy;
            this.timeoutMilliseconds = timeoutMilliseconds;
        }

        public async Task<Tuple<bool, string>> TryReadFile(string sourcePath)
        {
            // any recently updated file will likely still be locked by the other
            // process. This will throw an exception. The recommended solution appears to be to
            // wait a short period of time and try again
            bool fileBusy = true;
            bool fileExists = true;
            bool keepTrying = true;
            string pageText = string.Empty;
            DateTime startTime = DateTime.Now;
            while (fileExists && fileBusy && keepTrying)
            {
                try
                {
                    using (System.IO.StreamReader reader = System.IO.File.OpenText(sourcePath))
                    {
                        pageText = await reader.ReadToEndAsync();
                        fileBusy = false;
                    }
                }
                catch (System.IO.FileNotFoundException)
                {
                    // the file doesn't exist, presumably it has been deleted but we
                    // haven't processed the delete event yet
                    fileExists = false;
                }
                catch (System.IO.IOException ex)
                {
                    // file busy, this is quite likely to happen when a file has just been updated,
                    // try again

                    // TODO: Add proper logger
                    Console.WriteLine("IOException: " + ex.Message);

                    switch (policy)
                    {
                        case FileReaderPolicy.Immediate:
                            keepTrying = false;
                            break;
                        case FileReaderPolicy.LimitedBlock:
                            TimeSpan failTime = DateTime.Now - startTime;

                            if (failTime.TotalMilliseconds > timeoutMilliseconds)
                            {
                                keepTrying = false;
                            }
                            break;
                        case FileReaderPolicy.IndefiniteBlock:
                            // just keep trying ...
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(policy.ToString());
                    }
                }
            }

            return new Tuple<bool, string>(fileExists && !fileBusy, pageText);
        }
    }
}
