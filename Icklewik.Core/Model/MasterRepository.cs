using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Icklewik.Core.Model
{
    /// <summary>
    /// Holds the canonical representation of a wiki.
    /// 
    /// Changes to the repository are broadcast by firing the relevant events
    /// </summary>
    public class MasterRepository : WikiRepository<NullLockPolicy>, IWikiModelEventSource
    {
        public MasterRepository(string fileExtension)
            : base(fileExtension)
        {
        }

        public event Action<object, EventSourceInitialisedArgs> EventSourceStarted;

        public event Action<object, WikiModelEventArgs> PageAdded;
        public event Action<object, WikiModelEventArgs> PageUpdated;
        public event Action<object, WikiModelEventArgs> PageDeleted;
        public event Action<object, WikiModelEventArgs> PageMoved;

        public event Action<object, WikiModelEventArgs> DirectoryAdded;
        public event Action<object, WikiModelEventArgs> DirectoryUpdated;
        public event Action<object, WikiModelEventArgs> DirectoryDeleted;
        public event Action<object, WikiModelEventArgs> DirectoryMoved;

        public void Init(string rootSourcePath, string rootWikiPath, IEnumerable<string> initialFiles)
        {
            // create the root directory as as special case
            AddDirectory(PathHelper.GetFullPath(rootSourcePath), new RootInfo { RootWikiPath = PathHelper.GetFullPath(rootWikiPath) });

            if (EventSourceStarted != null)
            {
                EventSourceStarted(this, new EventSourceInitialisedArgs());
            }

            // iterate over all directories and add all relevant files to the model (which will fire the relevant events and prompt the
            // generator to generate the wiki itself)
            foreach (string markdownFilePath in initialFiles.Select(path => PathHelper.GetFullPath(path)))
            {
                if (!UnderlyingModel.PathComparer.Equals(markdownFilePath, UnderlyingModel.RootSourcePath))
                {
                    AddPage(markdownFilePath);
                }
            }
        }

        protected override void HandlePageAdded(WikiPage page)
        {
            System.Console.WriteLine("{0} Firing event (PageAdded:{1}) on thread: {2}", DateTime.Now.Ticks, page.SourcePath, Thread.CurrentThread.Name);
            FireEvent(PageAdded, page);
        }

        protected override void HandlePageUpdated(WikiPage page)
        {
            System.Console.WriteLine("{0} Firing event (PageUpdated: {1}) on thread: {2}", DateTime.Now.Ticks, page.SourcePath, Thread.CurrentThread.Name);
            FireEvent(PageUpdated, page);
        }

        protected override void HandlePageDeleted(WikiPage page)
        {
            System.Console.WriteLine("{0} Firing event (PageDeleted: {1}) on thread: {2}", DateTime.Now.Ticks, page.SourcePath, Thread.CurrentThread.Name);
            FireEvent(PageDeleted, page);
        }

        protected override void HandlePageMoved(WikiPage page, string oldSourcePath, string oldWikiPath)
        {
            System.Console.WriteLine("{0} Firing event (PageMoved: {1}) on thread: {2}", DateTime.Now.Ticks, page.SourcePath, Thread.CurrentThread.Name);
            FireEvent(PageMoved, page, oldSourcePath, oldWikiPath);
        }

        protected override void HandleDirectoryAdded(WikiDirectory page)
        {
            System.Console.WriteLine("{0} Firing event (DirectoryAdded: {1}) on thread: {2}", DateTime.Now.Ticks, page.SourcePath, Thread.CurrentThread.Name);
            FireEvent(DirectoryAdded, page);
        }

        protected override void HandleDirectoryUpdated(WikiDirectory page)
        {
            System.Console.WriteLine("{0} Firing event (DirectoryUpdated: {1}) on thread: {2}", DateTime.Now.Ticks, page.SourcePath, Thread.CurrentThread.Name);
            FireEvent(DirectoryUpdated, page);
        }

        protected override void HandleDirectoryDeleted(WikiDirectory page)
        {
            System.Console.WriteLine("{0} Firing event (DirectoryDeleted: {1}) on thread: {2}", DateTime.Now.Ticks, page.SourcePath, Thread.CurrentThread.Name);
            FireEvent(DirectoryDeleted, page);
        }

        protected override void HandleDirectoryMoved(WikiDirectory page, string oldSourcePath, string oldWikiPath)
        {
            System.Console.WriteLine("{0} Firing event (DirectoryMoved: {1}) on thread: {2}", DateTime.Now.Ticks, page.SourcePath, Thread.CurrentThread.Name);
            FireEvent(DirectoryMoved, page, oldSourcePath, oldWikiPath);
        }

        private void FireEvent(Action<object, WikiModelEventArgs> eventToFire, WikiEntry entry, string oldSourcePath = "", string oldWikiPath = "")
        {
            if (eventToFire != null)
            {
                WikiModelEventArgs args = new WikiModelEventArgs(
                    sourcePath: entry.SourcePath,
                    wikiPath: entry.WikiPath,
                    wikiUrl: entry.WikiUrl,
                    oldSourcePath: oldSourcePath,
                    oldWikiPath: oldWikiPath);

                System.Console.WriteLine("{0} Firing event #{1} on thread: {2}", DateTime.Now.Ticks, args.Id, Thread.CurrentThread.Name);

                eventToFire(this, args);
            }
        }
    }

}
