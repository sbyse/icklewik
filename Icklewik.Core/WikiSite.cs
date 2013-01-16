using System.IO;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using System.Reactive;
using System.Threading;

namespace Icklewik.Core
{
    public class WikiSite : IDisposable
    {
        private static readonly TimeSpan FileSystemSampleTime = new TimeSpan(0, 0, 0, 0, 50);

        private WikiConfig wikiConfig;

        private WikiRepository model;
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

            model = new WikiRepository(wikiConfig.Convertor.FileExtension);

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
            SubscribeToEvent<WikiRepositoryEventArgs>(model, "DirectoryAdded", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.CreateDirectory(a.WikiPath), DirectoryAdded, args), (args) => args.MarkdownPath);
            SubscribeToEvent<WikiRepositoryEventArgs>(model, "DirectoryUpdated", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.UpdateDirectory(a.WikiPath), DirectoryUpdated, args), (args) => args.MarkdownPath);
            SubscribeToEvent<WikiRepositoryEventArgs>(model, "DirectoryDeleted", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.DeleteDirectory(a.WikiPath), DirectoryDeleted, args), (args) => args.MarkdownPath);
            SubscribeToEvent<WikiRepositoryEventArgs>(model, "DirectoryMoved", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.MoveDirectory(a.OldWikiPath, a.WikiPath), DirectoryMoved, args), (args) => args.MarkdownPath);
            SubscribeToEvent<WikiRepositoryEventArgs>(model, "PageAdded", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.CreatePage(a.WikiPath, a.MarkdownPath), PageAdded, args), (args) => args.MarkdownPath);
            SubscribeToEvent<WikiRepositoryEventArgs>(model, "PageUpdated", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.UpdatePage(a.WikiPath, a.MarkdownPath), PageUpdated, args), (args) => args.MarkdownPath);
            SubscribeToEvent<WikiRepositoryEventArgs>(model, "PageDeleted", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.DeletePage(a.WikiPath), PageDeleted, args), (args) => args.MarkdownPath);
            SubscribeToEvent<WikiRepositoryEventArgs>(model, "PageMoved", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.MovePage(a.OldWikiPath, a.WikiPath), PageMoved, args), (args) => args.MarkdownPath);

            // subscribe the model builder to file system events
            SubscribeToSampledEvent<FileSystemEventArgs>(fileWatcher, "Created", modelBuilderScheduler, (e) => model.AddPage(e.FullPath), (e) => e.FullPath, FileSystemSampleTime);
            SubscribeToSampledEvent<FileSystemEventArgs>(fileWatcher, "Changed", modelBuilderScheduler, (e) => model.UpdatePage(e.FullPath), (e) => e.FullPath, FileSystemSampleTime);
            SubscribeToSampledEvent<FileSystemEventArgs>(fileWatcher, "Deleted", modelBuilderScheduler, (e) => model.DeletePage(e.FullPath), (e) => e.FullPath, FileSystemSampleTime);
            SubscribeToSampledEvent<RenamedEventArgs>(fileWatcher, "Renamed", modelBuilderScheduler, (e) => model.RenamePage(e.OldFullPath, e.FullPath), (e) => e.FullPath, FileSystemSampleTime);
            SubscribeToSampledEvent<FileSystemEventArgs>(directoryWatcher, "Created", modelBuilderScheduler, (e) => model.AddDirectory(e.FullPath), (e) => e.FullPath, FileSystemSampleTime);
            SubscribeToSampledEvent<FileSystemEventArgs>(directoryWatcher, "Changed", modelBuilderScheduler, (e) => model.UpdateDirectory(e.FullPath), (e) => e.FullPath, FileSystemSampleTime);
            SubscribeToSampledEvent<FileSystemEventArgs>(directoryWatcher, "Deleted", modelBuilderScheduler, (e) => model.DeleteDirectory(e.FullPath), (e) => e.FullPath, FileSystemSampleTime);
            SubscribeToSampledEvent<RenamedEventArgs>(directoryWatcher, "Renamed", modelBuilderScheduler, (e) => model.RenameDirectory(e.OldFullPath, e.FullPath), (e) => e.FullPath, FileSystemSampleTime);

            // get the initial files
            IEnumerable<string> initialSourceFiles = Directory.EnumerateFiles(wikiConfig.RootSourcePath, wikiConfig.Convertor.FileSearchString, SearchOption.AllDirectories);

            // initialise the model
            model.Init(wikiConfig.RootSourcePath, wikiConfig.RootWikiPath, initialSourceFiles);

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

        private void SubscribeToEvent<TEventArgs>(object target, string eventName, IScheduler scheduler, Action<TEventArgs> eventAction, Func<TEventArgs, string> identityFunc) where TEventArgs : EventArgs
        {
            ObserveOnAndSubscribe(
                    Observable.FromEventPattern<TEventArgs>(target, eventName),
                    eventName,
                    scheduler,
                    eventAction,
                    identityFunc);
        }

        private void SubscribeToSampledEvent<TEventArgs>(object target, string eventName, IScheduler scheduler, Action<TEventArgs> eventAction, Func<TEventArgs, string> identityFunc, TimeSpan sampleTimeSpan) where TEventArgs : EventArgs
        {
            // TODO: Investigate
            // experimental method, tries to group identical events together so we don't have to worry about multiple firings
            // currently doesn't pass all unit tests so not used
            //ObserveOnAndSubscribe(
            //        Observable.FromEventPattern<TEventArgs>(target, eventName)
            //            .GroupBy(evt => identityFunc(evt.EventArgs))
            //            .SelectMany(grp => grp.Sample(sampleTimeSpan)),
            //        eventName,
            //        scheduler,
            //        eventAction,
            //        identityFunc);

            // use forward to standard SubscribeToEvent
            SubscribeToEvent(target, eventName, scheduler, eventAction, identityFunc);
        }

        private void ObserveOnAndSubscribe<TEventArgs>(IObservable<EventPattern<TEventArgs>> observable, string eventName, IScheduler scheduler, Action<TEventArgs> eventAction, Func<TEventArgs, string> identityFunc) where TEventArgs : EventArgs
        {
                observable
                    .ObserveOn(scheduler)
                    .Subscribe(evt =>
                    {
                        System.Console.WriteLine("{0} Handling event ({1}:{2}) on thread: {3}", DateTime.Now.Ticks, eventName, identityFunc(evt.EventArgs), Thread.CurrentThread.ManagedThreadId);
                        eventAction(evt.EventArgs);
                    });
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
