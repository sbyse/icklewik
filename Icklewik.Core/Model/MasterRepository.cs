using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Icklewik.Core.Source;

namespace Icklewik.Core.Model
{
    /// <summary>
    /// Holds the canonical representation of a wiki.
    /// 
    /// Changes to the repository are broadcast by firing the relevant events
    /// 
    /// Repository state is accessible via the thread safe async interface
    /// </summary>
    public class MasterRepository : WikiRepository<NullLockPolicy>, IAsyncModel, IWikiModelEvents, IDisposable
    {
        private EventLoopScheduler scheduler;

        public MasterRepository(string fileExtension)
            : base(fileExtension)
        {
            scheduler = new EventLoopScheduler(threadStart => new Thread(threadStart) { Name = "ModelBuilder" });
        }

        public event Action<object, EventSourceInitialisedArgs> EventSourceInitialised;

        public event Action<object, WikiModelEventArgs> PageAdded;
        public event Action<object, WikiModelEventArgs> PageUpdated;
        public event Action<object, WikiModelEventArgs> PageDeleted;
        public event Action<object, WikiModelEventArgs> PageMoved;

        public event Action<object, WikiModelEventArgs> DirectoryAdded;
        public event Action<object, WikiModelEventArgs> DirectoryUpdated;
        public event Action<object, WikiModelEventArgs> DirectoryDeleted;
        public event Action<object, WikiModelEventArgs> DirectoryMoved;

        public void Init(
            IWikiSourceEvents watcher,
            string rootSourcePath, 
            string rootWikiPath, 
            IEnumerable<string> initialFiles)
        {
            // subscribe the model builder to file system events
            EventHelper.SubscribeToWikiEvent<WikiSourceEventArgs>(watcher, "FileAdded", scheduler, (e) => AddPage(e.SourcePath));
            EventHelper.SubscribeToWikiEvent<WikiSourceEventArgs>(watcher, "FileUpdated", scheduler, (e) => UpdatePage(e.SourcePath));
            EventHelper.SubscribeToWikiEvent<WikiSourceEventArgs>(watcher, "FileDeleted", scheduler, (e) => DeletePage(e.SourcePath));
            EventHelper.SubscribeToWikiEvent<WikiSourceEventArgs>(watcher, "FileMoved", scheduler, (e) => RenamePage(e.OldSourcePath, e.SourcePath));
            EventHelper.SubscribeToWikiEvent<WikiSourceEventArgs>(watcher, "DirectoryAdded", scheduler, (e) => AddDirectory(e.SourcePath));
            EventHelper.SubscribeToWikiEvent<WikiSourceEventArgs>(watcher, "DirectoryUpdated", scheduler, (e) => UpdateDirectory(e.SourcePath));
            EventHelper.SubscribeToWikiEvent<WikiSourceEventArgs>(watcher, "DirectoryDeleted", scheduler, (e) => DeleteDirectory(e.SourcePath));
            EventHelper.SubscribeToWikiEvent<WikiSourceEventArgs>(watcher, "DirectoryMoved", scheduler, (e) => RenameDirectory(e.OldSourcePath, e.SourcePath));

            // create the root directory as as special case
            AddDirectory(PathHelper.GetFullPath(rootSourcePath), new RootInfo { RootWikiPath = PathHelper.GetFullPath(rootWikiPath) });

            // iterate over all directories and add all relevant files to the model (which will fire the relevant events and prompt the
            // generator to generate the wiki itself)
            foreach (string markdownFilePath in initialFiles.Select(path => PathHelper.GetFullPath(path)))
            {
                if (!UnderlyingModel.PathComparer.Equals(markdownFilePath, UnderlyingModel.RootSourcePath))
                {
                    AddPage(markdownFilePath);
                }
            }
            if (EventSourceInitialised != null)
            {
                EventSourceInitialised(this, new EventSourceInitialisedArgs());
            }
        }

        public void Dispose()
        {
            scheduler.Dispose();
        }

        public async Task<IEnumerable<ImmutableWikiEntry>> GetAvailableAssets()
        {
            return await Task.Run(() => UnderlyingModel.GetImmutableWikiEntries()); 
        }

        public async Task<Tuple<bool, ImmutableWikiDirectory>> GetDirectory(string fullPath)
        {
            return await Task.Run(() => 
                {
                    if (UnderlyingModel.ContainsAssetBySourcePath(fullPath))
                    {
                        ImmutableWikiEntry entry = UnderlyingModel.GetImmutableAssetBySourcePath(fullPath);
                        if (entry is ImmutableWikiDirectory)
                        {
                            return new Tuple<bool, ImmutableWikiDirectory>(true, entry as ImmutableWikiDirectory);
                        }
                        else
                        {
                            return new Tuple<bool, ImmutableWikiDirectory>(false, null);
                        }
                    }
                    else
                    {
                        return new Tuple<bool, ImmutableWikiDirectory>(false, null);
                    }
                }); 
        }

        public async Task<Tuple<bool, ImmutableWikiPage>> GetPageByWikiUrl(string url)
        {
            return await Task.Run(() =>
            {
                if (UnderlyingModel.ContainsAssetBySourcePath(url))
                {
                    ImmutableWikiEntry entry = UnderlyingModel.GetImmutableAssetByWikiUrl(url);
                    if (entry is ImmutableWikiPage)
                    {
                        return new Tuple<bool, ImmutableWikiPage>(true, entry as ImmutableWikiPage);
                    }
                    else
                    {
                        return new Tuple<bool, ImmutableWikiPage>(false, null);
                    }
                }
                else
                {
                    return new Tuple<bool, ImmutableWikiPage>(false, null);
                }
            }); 
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

        protected override void HandlePageMoved(WikiPage page, string oldSourcePath, string oldWikiPath, string oldWikiUrl)
        {
            System.Console.WriteLine("{0} Firing event (PageMoved: {1}) on thread: {2}", DateTime.Now.Ticks, page.SourcePath, Thread.CurrentThread.Name);
            FireEvent(PageMoved, page, oldSourcePath, oldWikiPath, oldWikiUrl);
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

        protected override void HandleDirectoryMoved(WikiDirectory page, string oldSourcePath, string oldWikiPath, string oldWikiUrl)
        {
            System.Console.WriteLine("{0} Firing event (DirectoryMoved: {1}) on thread: {2}", DateTime.Now.Ticks, page.SourcePath, Thread.CurrentThread.Name);
            FireEvent(DirectoryMoved, page, oldSourcePath, oldWikiPath, oldWikiUrl);
        }

        private void FireEvent(Action<object, WikiModelEventArgs> eventToFire, WikiEntry entry, string oldSourcePath = "", string oldWikiPath = "", string oldWikiUrl = "")
        {
            if (eventToFire != null)
            {
                WikiModelEventArgs args = new WikiModelEventArgs(
                    sourcePath: entry.SourcePath,
                    wikiPath: entry.WikiPath,
                    wikiUrl: entry.WikiUrl,
                    oldSourcePath: oldSourcePath,
                    oldWikiPath: oldWikiPath,
                    oldWikiUrl: oldWikiUrl);

                System.Console.WriteLine("{0} Firing event #{1} on thread: {2}", DateTime.Now.Ticks, args.Id, Thread.CurrentThread.Name);

                eventToFire(this, args);
            }
        }
    }

}
