using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Concurrency;
using System.Threading;
using Icklewik.Core.Model;

namespace Icklewik.Core.Site
{
    public class WikiSite : IWikiSiteEventSource, IDisposable
    {
        private static readonly TimeSpan FileSystemSampleTime = new TimeSpan(0, 0, 0, 0, 50);

        private WikiConfig wikiConfig;

        private MasterRepository repository;
        private IList<SlaveRepository> slaves;

        private WikiGenerator generator;
        private FileSystemWatcher fileWatcher;
        private FileSystemWatcher directoryWatcher;

        // we maintain two separate event loops to keep model and site 
        // as decoupled as possible
        private EventLoopScheduler modelBuilderScheduler;
        private EventLoopScheduler siteGeneratorScheduler;

        public WikiSite(WikiConfig config)
        {
            wikiConfig = config;

            repository = new MasterRepository(wikiConfig.Convertor.FileExtension);
            slaves = new List<SlaveRepository>();

            generator = new WikiGenerator(wikiConfig.Convertor, wikiConfig.RootWikiPath);

            // now we create a file system watcher to make sure we stay in sync with future changes
            fileWatcher = new FileSystemWatcher();
            fileWatcher.Path = wikiConfig.RootSourcePath;
            fileWatcher.Filter = wikiConfig.Convertor.FileSearchString;
            fileWatcher.IncludeSubdirectories = true;
            fileWatcher.InternalBufferSize = 32768;

            fileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;

            directoryWatcher = new FileSystemWatcher();
            directoryWatcher.Path = wikiConfig.RootSourcePath;
            directoryWatcher.IncludeSubdirectories = true;
            directoryWatcher.NotifyFilter = NotifyFilters.DirectoryName;
            directoryWatcher.InternalBufferSize = 32768;

            modelBuilderScheduler = new EventLoopScheduler(threadStart => new Thread(threadStart) { Name = "ModelBuilder" });
            siteGeneratorScheduler = new EventLoopScheduler(threadStart => new Thread(threadStart) { Name = "SiteGenerator" });
        }

        // We expose the site events for others to listen to, they are forwarded on from this
        public event Action<object, WikiSiteEventArgs> PageAdded;
        public event Action<object, WikiSiteEventArgs> PageUpdated;
        public event Action<object, WikiSiteEventArgs> PageDeleted;
        public event Action<object, WikiSiteEventArgs> PageMoved;

        public event Action<object, WikiSiteEventArgs> DirectoryAdded;
        public event Action<object, WikiSiteEventArgs> DirectoryUpdated;
        public event Action<object, WikiSiteEventArgs> DirectoryDeleted;
        public event Action<object, WikiSiteEventArgs> DirectoryMoved;

        public string Name
        {
            get
            {
                return wikiConfig.SiteName;
            }
        }

        public void RegisterSlaveRepository(SlaveRepository slaveRepo, IScheduler scheduler)
        {
            slaveRepo.Init(repository, scheduler);

            slaves.Add(slaveRepo);
        }

        public void Start()
        {
            System.Console.WriteLine(string.Format("Starting on thread: {0}", Thread.CurrentThread.Name));

            // subscribe the model builder to file system events
            EventHelper.SubscribeToSampledEvent<FileSystemEventArgs>(fileWatcher, "Created", modelBuilderScheduler, (e) => repository.AddPage(e.FullPath), (e) => e.FullPath, FileSystemSampleTime);
            EventHelper.SubscribeToSampledEvent<FileSystemEventArgs>(fileWatcher, "Changed", modelBuilderScheduler, (e) => repository.UpdatePage(e.FullPath), (e) => e.FullPath, FileSystemSampleTime);
            EventHelper.SubscribeToSampledEvent<FileSystemEventArgs>(fileWatcher, "Deleted", modelBuilderScheduler, (e) => repository.DeletePage(e.FullPath), (e) => e.FullPath, FileSystemSampleTime);
            EventHelper.SubscribeToSampledEvent<RenamedEventArgs>(fileWatcher, "Renamed", modelBuilderScheduler, (e) => repository.RenamePage(e.OldFullPath, e.FullPath), (e) => e.FullPath, FileSystemSampleTime);
            EventHelper.SubscribeToSampledEvent<FileSystemEventArgs>(directoryWatcher, "Created", modelBuilderScheduler, (e) => repository.AddDirectory(e.FullPath), (e) => e.FullPath, FileSystemSampleTime);
            EventHelper.SubscribeToSampledEvent<FileSystemEventArgs>(directoryWatcher, "Changed", modelBuilderScheduler, (e) => repository.UpdateDirectory(e.FullPath), (e) => e.FullPath, FileSystemSampleTime);
            EventHelper.SubscribeToSampledEvent<FileSystemEventArgs>(directoryWatcher, "Deleted", modelBuilderScheduler, (e) => repository.DeleteDirectory(e.FullPath), (e) => e.FullPath, FileSystemSampleTime);
            EventHelper.SubscribeToSampledEvent<RenamedEventArgs>(directoryWatcher, "Renamed", modelBuilderScheduler, (e) => repository.RenameDirectory(e.OldFullPath, e.FullPath), (e) => e.FullPath, FileSystemSampleTime);

            // subscribe the site generator model events and forward events to our own handlers
            EventHelper.SubscribeToWikiEvent<WikiModelEventArgs>(repository, "DirectoryAdded", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.CreateDirectory(a.WikiPath), args));
            EventHelper.SubscribeToWikiEvent<WikiModelEventArgs>(repository, "DirectoryUpdated", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.UpdateDirectory(a.WikiPath), args));
            EventHelper.SubscribeToWikiEvent<WikiModelEventArgs>(repository, "DirectoryDeleted", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.DeleteDirectory(a.WikiPath), args));
            EventHelper.SubscribeToWikiEvent<WikiModelEventArgs>(repository, "DirectoryMoved", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.MoveDirectory(a.OldWikiPath, a.WikiPath), args));
            EventHelper.SubscribeToWikiEvent<WikiModelEventArgs>(repository, "PageAdded", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.CreatePage(a.WikiPath, a.SourcePath), args));
            EventHelper.SubscribeToWikiEvent<WikiModelEventArgs>(repository, "PageUpdated", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.UpdatePage(a.WikiPath, a.SourcePath), args));
            EventHelper.SubscribeToWikiEvent<WikiModelEventArgs>(repository, "PageDeleted", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.DeletePage(a.WikiPath), args));
            EventHelper.SubscribeToWikiEvent<WikiModelEventArgs>(repository, "PageMoved", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.MovePage(a.OldWikiPath, a.WikiPath), args));

            // subscribe to the site generation events to forward on
            EventHelper.SubscribeToWikiEvent<WikiSiteEventArgs>(generator, "DirectoryAdded", siteGeneratorScheduler, (args) => HandleSiteUpdate(DirectoryAdded, args));
            EventHelper.SubscribeToWikiEvent<WikiSiteEventArgs>(generator, "DirectoryUpdated", siteGeneratorScheduler, (args) => HandleSiteUpdate(DirectoryUpdated, args));
            EventHelper.SubscribeToWikiEvent<WikiSiteEventArgs>(generator, "DirectoryDeleted", siteGeneratorScheduler, (args) => HandleSiteUpdate(DirectoryDeleted, args));
            EventHelper.SubscribeToWikiEvent<WikiSiteEventArgs>(generator, "DirectoryMoved", siteGeneratorScheduler, (args) => HandleSiteUpdate(DirectoryMoved, args));
            EventHelper.SubscribeToWikiEvent<WikiSiteEventArgs>(generator, "PageAdded", siteGeneratorScheduler, (args) => HandleSiteUpdate(PageAdded, args));
            EventHelper.SubscribeToWikiEvent<WikiSiteEventArgs>(generator, "PageUpdated", siteGeneratorScheduler, (args) => HandleSiteUpdate(PageUpdated, args));
            EventHelper.SubscribeToWikiEvent<WikiSiteEventArgs>(generator, "PageDeleted", siteGeneratorScheduler, (args) => HandleSiteUpdate(PageDeleted, args));
            EventHelper.SubscribeToWikiEvent<WikiSiteEventArgs>(generator, "PageMoved", siteGeneratorScheduler, (args) => HandleSiteUpdate(PageMoved, args));

            // subscribe to initialisation event
            EventHelper.SubscribeToWikiEvent<EventSourceInitialisedArgs>(repository, "EventSourceStarted", Scheduler.CurrentThread, (e) => HandleRepositoryInitialised(e));

            // get the initial files
            IEnumerable<string> initialSourceFiles = Directory.EnumerateFiles(wikiConfig.RootSourcePath, wikiConfig.Convertor.FileSearchString, SearchOption.AllDirectories);

            // initialise the model on the model builder thread
            modelBuilderScheduler.Schedule(() =>
                {
                    repository.Init(wikiConfig.RootSourcePath, wikiConfig.RootWikiPath, initialSourceFiles);
                });
        }

        public void Dispose()
        {
            slaves.Clear();

            modelBuilderScheduler.Dispose();
            siteGeneratorScheduler.Dispose();

            fileWatcher.EnableRaisingEvents = false;
            directoryWatcher.EnableRaisingEvents = false;

            fileWatcher.Dispose();
            directoryWatcher.Dispose();
        }

        private void HandleRepositoryInitialised(EventSourceInitialisedArgs args)
        {
            // Begin watching the file system
            fileWatcher.EnableRaisingEvents = true;
            directoryWatcher.EnableRaisingEvents = true;
        }

        private void HandleModelUpdate(Action<WikiModelEventArgs> generatorAction, WikiModelEventArgs args)
        {
            generatorAction(args);
        }

        private void HandleSiteUpdate(Action<object, WikiSiteEventArgs> eventToFire, WikiSiteEventArgs args)
        {
            if (eventToFire != null)
            {
                eventToFire(this, args);
            }
        }
    }
}
