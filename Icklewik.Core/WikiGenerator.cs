using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Icklewik.Core
{
    /// <summary>
    /// 
    /// </summary>
    public class WikiGenerator
    {
        private Convertor convertor;

        public WikiGenerator(Convertor sourceConvertor)
        {
            convertor = sourceConvertor;
        }

        public void CreateDirectory(string wikiPath)
        {
            Directory.CreateDirectory(wikiPath);
        }

        public void UpdateDirectory(string wikiPath)
        {
        }

        public void DeleteDirectory(string wikiPath)
        {
            if (Directory.Exists(wikiPath))
            {
                Directory.Delete(wikiPath, true);
            }
        }

        public void MoveDirectory(string oldPath, string wikiPath)
        {
            Directory.Move(oldPath, wikiPath);
        }

        public void CreatePage(string wikiPath, string sourcePath)
        {
            UpdatePage(wikiPath, sourcePath);
        }

        public void UpdatePage(string wikiPath, string sourcePath)
        {
            // any recently updated file will likely still be locked by the other
            // process. This will throw an exception. The recommended solution appears to be to
            // wait a short period of time and try again
            bool fileBusy = true;
            bool fileExists = true;
            string pageText = "";
            while (fileExists && fileBusy)
            {
                try
                {
                    pageText = File.ReadAllText(sourcePath);
                    fileBusy = false;
                }
                catch (FileNotFoundException)
                {
                    // the file doesn't exist, presumably it has been deleted but we
                    // haven't processed the delete event yet
                    fileExists = false;
                }
                catch (IOException ex)
                {
                    // file busy, this is quite likely to happen when a file has just been updated,
                    // try again
                    Console.WriteLine("IOException: " + ex.Message);
                }
            }

            if (fileExists)
            {
                // actually parse the markdown
                string convertedHtml = convertor.Convert(pageText);

                // and write to the wiki location (write directory tree first if required)
                string parentPath = Directory.GetParent(wikiPath).FullName;
                while (!Directory.Exists(parentPath))
                {
                    Directory.CreateDirectory(parentPath);

                    parentPath = Directory.GetParent(parentPath).FullName;
                }

                File.WriteAllText(wikiPath, convertedHtml, Encoding.UTF8);
            }
        }

        public void DeletePage(string wikiPath)
        {
            if (File.Exists(wikiPath))
            {
                File.Delete(wikiPath);
            }
        }

        public void MovePage(string oldPath, string wikiPath)
        {
            File.Move(oldPath, wikiPath);
        }
    }
}
