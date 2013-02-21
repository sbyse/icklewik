using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Icklewik.Core.Site
{
    /// <summary>
    /// 
    /// </summary>
    public class WikiGenerator : IWikiSiteEventSource
    {
        private Convertor convertor;
        private string rootWikiPath;

        public WikiGenerator(Convertor sourceConvertor, string rootPath)
        {
            convertor = sourceConvertor;
            rootWikiPath = rootPath;
        }

        public event Action<object, WikiSiteEventArgs> PageAdded;
        public event Action<object, WikiSiteEventArgs> PageUpdated;
        public event Action<object, WikiSiteEventArgs> PageDeleted;
        public event Action<object, WikiSiteEventArgs> PageMoved;
        public event Action<object, WikiSiteEventArgs> DirectoryAdded;
        public event Action<object, WikiSiteEventArgs> DirectoryUpdated;
        public event Action<object, WikiSiteEventArgs> DirectoryDeleted;
        public event Action<object, WikiSiteEventArgs> DirectoryMoved;

        public void CreateDirectory(string wikiPath)
        {
            CreateDirectoryAndParents(wikiPath);
        }

        public void UpdateDirectory(string wikiPath)
        {
            // TODO: Do we need to do anything here?

            FireEvent(DirectoryUpdated, wikiPath);
        }

        public void DeleteDirectory(string wikiPath)
        {
            if (Directory.Exists(wikiPath))
            {
                Directory.Delete(wikiPath, true);

                FireEvent(DirectoryDeleted, wikiPath);
            }
        }

        public void MoveDirectory(string oldPath, string wikiPath)
        {
            Directory.Move(oldPath, wikiPath);

            FireEvent(DirectoryMoved, wikiPath, oldPath);
        }

        public void CreatePage(string wikiPath, string sourcePath)
        {
            CreateOrUpdatePage(PageAdded, wikiPath, sourcePath);
        }

        public void UpdatePage(string wikiPath, string sourcePath)
        {
            CreateOrUpdatePage(PageUpdated, wikiPath, sourcePath);
        }

        private void CreateOrUpdatePage(Action<object, WikiSiteEventArgs> eventToFire, string wikiPath, string sourcePath)
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
                CreateDirectoryAndParents(Directory.GetParent(wikiPath).FullName);

                File.WriteAllText(wikiPath, convertedHtml, Encoding.UTF8);

                FireEvent(eventToFire, wikiPath);
            }
        }

        public void DeletePage(string wikiPath)
        {
            if (File.Exists(wikiPath))
            {
                File.Delete(wikiPath);

                FireEvent(PageDeleted, wikiPath);
            }
        }

        public void MovePage(string oldPath, string wikiPath)
        {
            File.Move(oldPath, wikiPath);

            FireEvent(PageMoved, wikiPath, oldPath);
        }

        private void CreateDirectoryAndParents(string wikiPath)
        {
            string directoryToCreate = wikiPath;
            while (!Directory.Exists(directoryToCreate))
            {
                Directory.CreateDirectory(directoryToCreate);

                FireEvent(DirectoryAdded, directoryToCreate);

                directoryToCreate = Directory.GetParent(directoryToCreate).FullName;
            }
        }

        private void FireEvent(Action<object, WikiSiteEventArgs> eventToFire, string wikiPath, string oldWikiPath = "")
        {
            if (eventToFire != null && wikiPath.StartsWith(rootWikiPath))
            {
                // calculate the urls
                string wikiUrl = PathHelper.GetWikiUrl(rootWikiPath, wikiPath);
                string oldWikiUrl = "";

                if (!string.IsNullOrWhiteSpace(oldWikiPath) && oldWikiPath.StartsWith(rootWikiPath))
                {
                    oldWikiUrl = PathHelper.GetWikiUrl(rootWikiPath, oldWikiPath);
                }

                WikiSiteEventArgs args = new WikiSiteEventArgs(
                    wikiUrl: wikiUrl,
                    oldWikiUrl: oldWikiUrl);

                System.Console.WriteLine("{0} Firing event #{1} on thread: {2}", DateTime.Now.Ticks, args.Id, Thread.CurrentThread.Name);

                eventToFire(this, args);
            }
        }
    }
}
