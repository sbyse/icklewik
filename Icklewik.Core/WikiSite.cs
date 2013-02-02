using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Concurrency;
using System.Threading;
using Icklewik.Core.Model;

namespace Icklewik.Core
{
    public class WikiSite : IWikiModelEventSource, IDisposable
    {
        private static readonly TimeSpan FileSystemSampleTime = new TimeSpan(0, 0, 0, 0, 50);

        private WikiConfig wikiConfig;

        private MasterRepository repository;
        private WikiGenerator generator;
        private FileSystemWatcher fileWatcher;
        private FileSystemWatcher directoryWatcher;

        // we maintain two separate event loops to keep source and site 
        // as decoupled as possible
        private EventLoopScheduler modelBuilderScheduler;
        private EventLoopScheduler siteGeneratorScheduler;

        public WikiSite(WikiConfig config)
        {
            wikiConfig = config;

            repository = new MasterRepository(wikiConfig.Convertor.FileExtension);

            generator = new WikiGenerator(wikiConfig.Convertor);

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

            modelBuilderScheduler = new EventLoopScheduler();
            siteGeneratorScheduler = new EventLoopScheduler();
        }

        // We expose the model events for others to listen to, they are forwarded on from this
        public event Action<object, WikiRepositoryEventArgs> PageAdded;
        public event Action<object, WikiRepositoryEventArgs> PageUpdated;
        public event Action<object, WikiRepositoryEventArgs> PageDeleted;
        public event Action<object, WikiRepositoryEventArgs> PageMoved;

        public event Action<object, WikiRepositoryEventArgs> DirectoryAdded;
        public event Action<object, WikiRepositoryEventArgs> DirectoryUpdated;
        public event Action<object, WikiRepositoryEventArgs> DirectoryDeleted;
        public event Action<object, WikiRepositoryEventArgs> DirectoryMoved;

        public string Name
        {
            get
            {
                return wikiConfig.SiteName;
            }
        }

        public void Start()
        {
            System.Console.WriteLine(string.Format("Starting on thread: {0}", Thread.CurrentThread.ManagedThreadId));

            // subscribe the site generator model events and forward events to our own handlers
            EventHelper.SubscribeToEvent<WikiRepositoryEventArgs>(repository, "DirectoryAdded", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.CreateDirectory(a.WikiPath), DirectoryAdded, args), (args) => args.SourcePath);
            EventHelper.SubscribeToEvent<WikiRepositoryEventArgs>(repository, "DirectoryUpdated", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.UpdateDirectory(a.WikiPath), DirectoryUpdated, args), (args) => args.SourcePath);
            EventHelper.SubscribeToEvent<WikiRepositoryEventArgs>(repository, "DirectoryDeleted", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.DeleteDirectory(a.WikiPath), DirectoryDeleted, args), (args) => args.SourcePath);
            EventHelper.SubscribeToEvent<WikiRepositoryEventArgs>(repository, "DirectoryMoved", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.MoveDirectory(a.OldWikiPath, a.WikiPath), DirectoryMoved, args), (args) => args.SourcePath);
            EventHelper.SubscribeToEvent<WikiRepositoryEventArgs>(repository, "PageAdded", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.CreatePage(a.WikiPath, a.SourcePath), PageAdded, args), (args) => args.SourcePath);
            EventHelper.SubscribeToEvent<WikiRepositoryEventArgs>(repository, "PageUpdated", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.UpdatePage(a.WikiPath, a.SourcePath), PageUpdated, args), (args) => args.SourcePath);
            EventHelper.SubscribeToEvent<WikiRepositoryEventArgs>(repository, "PageDeleted", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.DeletePage(a.WikiPath), PageDeleted, args), (args) => args.SourcePath);
            EventHelper.SubscribeToEvent<WikiRepositoryEventArgs>(repository, "PageMoved", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.MovePage(a.OldWikiPath, a.WikiPath), PageMoved, args), (args) => args.SourcePath);

            // subscribe the model builder to file system events
            EventHelper.SubscribeToSampledEvent<FileSystemEventArgs>(fileWatcher, "Created", modelBuilderScheduler, (e) => repository.AddPage(e.FullPath), (e) => e.FullPath, FileSystemSampleTime);
            EventHelper.SubscribeToSampledEvent<FileSystemEventArgs>(fileWatcher, "Changed", modelBuilderScheduler, (e) => repository.UpdatePage(e.FullPath), (e) => e.FullPath, FileSystemSampleTime);
            EventHelper.SubscribeToSampledEvent<FileSystemEventArgs>(fileWatcher, "Deleted", modelBuilderScheduler, (e) => repository.DeletePage(e.FullPath), (e) => e.FullPath, FileSystemSampleTime);
            EventHelper.SubscribeToSampledEvent<RenamedEventArgs>(fileWatcher, "Renamed", modelBuilderScheduler, (e) => repository.RenamePage(e.OldFullPath, e.FullPath), (e) => e.FullPath, FileSystemSampleTime);
            EventHelper.SubscribeToSampledEvent<FileSystemEventArgs>(directoryWatcher, "Created", modelBuilderScheduler, (e) => repository.AddDirectory(e.FullPath), (e) => e.FullPath, FileSystemSampleTime);
            EventHelper.SubscribeToSampledEvent<FileSystemEventArgs>(directoryWatcher, "Changed", modelBuilderScheduler, (e) => repository.UpdateDirectory(e.FullPath), (e) => e.FullPath, FileSystemSampleTime);
            EventHelper.SubscribeToSampledEvent<FileSystemEventArgs>(directoryWatcher, "Deleted", modelBuilderScheduler, (e) => repository.DeleteDirectory(e.FullPath), (e) => e.FullPath, FileSystemSampleTime);
            EventHelper.SubscribeToSampledEvent<RenamedEventArgs>(directoryWatcher, "Renamed", modelBuilderScheduler, (e) => repository.RenameDirectory(e.OldFullPath, e.FullPath), (e) => e.FullPath, FileSystemSampleTime);

            // get the initial files
            IEnumerable<string> initialSourceFiles = Directory.EnumerateFiles(wikiConfig.RootSourcePath, wikiConfig.Convertor.FileSearchString, SearchOption.AllDirectories);

            // initialise the model
            repository.Init(wikiConfig.RootSourcePath, wikiConfig.RootWikiPath, initialSourceFiles);

            // Begin watching the file system
            fileWatcher.EnableRaisingEvents = true;
            directoryWatcher.EnableRaisingEvents = true;
        }

        public void Dispose()
        {
            modelBuilderScheduler.Dispose();
            siteGeneratorScheduler.Dispose();

            fileWatcher.EnableRaisingEvents = false;
            directoryWatcher.EnableRaisingEvents = false;

            fileWatcher.Dispose();
            directoryWatcher.Dispose();
        }

        private void HandleModelUpdate(Action<WikiRepositoryEventArgs> generatorAction, Action<object, WikiRepositoryEventArgs> postGenerationEventToFire, WikiRepositoryEventArgs args)
        {
            // TODO: This is a little too simplistic. It may be that it would be better to have an
            // event fired by the generator itself, then we could throttle events on a single
            // page so we don't get 3 or 4 events at the same time
            generatorAction(args);

            if (postGenerationEventToFire != null)
            {
                postGenerationEventToFire(this, args);
            }
        }
    }
}
