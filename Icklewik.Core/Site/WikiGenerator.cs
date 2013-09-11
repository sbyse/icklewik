using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Icklekwik.Core.Cache;
using Icklewik.Core.File;

namespace Icklewik.Core.Site
{
    /// <summary>
    /// Updates the wiki site based on source files
    /// </summary>
    public class WikiGenerator : IWikiSiteEvents
    {
        private Convertor convertor;
        private string rootWikiPath;
        private IPageCache pageCache;
        private FileReader fileReader;

        public WikiGenerator(Convertor sourceConvertor, string rootPath, IPageCache pageCache)
        {
            this.convertor = sourceConvertor;
            this.rootWikiPath = rootPath;
            this.pageCache = pageCache;
            this.fileReader = new FileReader(FileReaderPolicy.LimitedBlock, 500);
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

        public void CreatePage(string wikiPath, string sourcePath, string wikiUrl)
        {
            CreateOrUpdatePage(PageAdded, wikiPath, sourcePath, wikiUrl);
        }

        public void UpdatePage(string wikiPath, string sourcePath, string wikiUrl)
        {
            CreateOrUpdatePage(PageUpdated, wikiPath, sourcePath, wikiUrl);
        }

        private async void CreateOrUpdatePage(Action<object, WikiSiteEventArgs> eventToFire, string wikiPath, string sourcePath, string wikiUrl)
        {
            var result = await fileReader.TryReadFile(sourcePath);
            if (result.Item1)
            {
                ConvertAndWritePage(wikiPath, wikiUrl, result.Item2);
                FireEvent(eventToFire, wikiPath);
            }
        }

        private void ConvertAndWritePage(string wikiPath, string wikiUrl, string pageText)
        {
            // actually parse the markdown
            string convertedHtml = convertor.Convert(pageText);

            // and write to the wiki location (write directory tree first if required)
            CreateDirectoryAndParents(Directory.GetParent(wikiPath).FullName);

            // write to the cache
            pageCache.CachePageContents(wikiUrl, convertedHtml);

            // write to file
            System.IO.File.WriteAllText(wikiPath, convertedHtml, Encoding.UTF8);
        }

        public void DeletePage(string wikiPath, string wikiUrl)
        {
            if (System.IO.File.Exists(wikiPath))
            {
                System.IO.File.Delete(wikiPath);

                pageCache.DeleteFromCache(wikiUrl);

                FireEvent(PageDeleted, wikiPath);
            }
        }

        public async void MovePage(string oldWikiPath, string newWikiPath, string oldWikiUrl, string newWikiUrl)
        {
            System.IO.File.Move(oldWikiPath, newWikiPath);

            string contents;
            if (pageCache.TryGetContents(oldWikiUrl, out contents))
            {
                pageCache.DeleteFromCache(oldWikiUrl);
                pageCache.CachePageContents(newWikiUrl, contents);
            }
            else
            {
                var result = await fileReader.TryReadFile(newWikiPath);

                if (result.Item1)
                {
                    ConvertAndWritePage(newWikiPath, newWikiUrl, result.Item2);
                }
            }

            FireEvent(PageMoved, newWikiPath, oldWikiPath);
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
