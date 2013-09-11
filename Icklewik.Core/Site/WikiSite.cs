using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Concurrency;
using System.Threading;
using Icklekwik.Core.Cache;
using Icklewik.Core.Model;
using Icklewik.Core.Source;

namespace Icklewik.Core.Site
{
    public class WikiSite : IWikiSiteEvents, IDisposable
    {
        private WikiConfig wikiConfig;

        private MasterRepository repository;

        private ISourceWatcher watcher;
        private WikiGenerator generator;

        // we maintain two separate event loops to keep model and site 
        // as decoupled as possible
        private EventLoopScheduler siteGeneratorScheduler;

        public WikiSite(WikiConfig config, MasterRepository masterRepository, ISourceWatcher sourceWatcher, IPageCache pageCache)
        {
            wikiConfig = config;

            repository = masterRepository;

            watcher = sourceWatcher;
            generator = new WikiGenerator(wikiConfig.Convertor, wikiConfig.RootWikiPath, pageCache);

            siteGeneratorScheduler = new EventLoopScheduler(threadStart => new Thread(threadStart) { Name = "SiteGenerator" });
        }

        public event EventHandler InitialisationComplete;

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

        public void Start()
        {
            System.Console.WriteLine(string.Format("Starting on thread: {0}", Thread.CurrentThread.Name));

            // subscribe the site generator model events and forward events to our own handlers
            EventHelper.SubscribeToWikiEvent<WikiModelEventArgs>(repository, "DirectoryAdded", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.CreateDirectory(a.WikiPath), args));
            EventHelper.SubscribeToWikiEvent<WikiModelEventArgs>(repository, "DirectoryUpdated", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.UpdateDirectory(a.WikiPath), args));
            EventHelper.SubscribeToWikiEvent<WikiModelEventArgs>(repository, "DirectoryDeleted", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.DeleteDirectory(a.WikiPath), args));
            EventHelper.SubscribeToWikiEvent<WikiModelEventArgs>(repository, "DirectoryMoved", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.MoveDirectory(a.OldWikiPath, a.WikiPath), args));
            EventHelper.SubscribeToWikiEvent<WikiModelEventArgs>(repository, "PageAdded", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.CreatePage(a.WikiPath, a.SourcePath, a.WikiUrl), args));
            EventHelper.SubscribeToWikiEvent<WikiModelEventArgs>(repository, "PageUpdated", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.UpdatePage(a.WikiPath, a.SourcePath, a.WikiUrl), args));
            EventHelper.SubscribeToWikiEvent<WikiModelEventArgs>(repository, "PageDeleted", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.DeletePage(a.WikiPath, a.WikiUrl), args));
            EventHelper.SubscribeToWikiEvent<WikiModelEventArgs>(repository, "PageMoved", siteGeneratorScheduler, (args) => HandleModelUpdate(a => generator.MovePage(a.OldWikiPath, a.WikiPath, a.OldWikiUrl, a.WikiUrl), args));

            // subscribe to the site generation events to forward on to anyone else who's interested
            EventHelper.SubscribeToWikiEvent<WikiSiteEventArgs>(generator, "DirectoryAdded", siteGeneratorScheduler, (args) => HandleSiteUpdate(DirectoryAdded, args));
            EventHelper.SubscribeToWikiEvent<WikiSiteEventArgs>(generator, "DirectoryUpdated", siteGeneratorScheduler, (args) => HandleSiteUpdate(DirectoryUpdated, args));
            EventHelper.SubscribeToWikiEvent<WikiSiteEventArgs>(generator, "DirectoryDeleted", siteGeneratorScheduler, (args) => HandleSiteUpdate(DirectoryDeleted, args));
            EventHelper.SubscribeToWikiEvent<WikiSiteEventArgs>(generator, "DirectoryMoved", siteGeneratorScheduler, (args) => HandleSiteUpdate(DirectoryMoved, args));
            EventHelper.SubscribeToWikiEvent<WikiSiteEventArgs>(generator, "PageAdded", siteGeneratorScheduler, (args) => HandleSiteUpdate(PageAdded, args));
            EventHelper.SubscribeToWikiEvent<WikiSiteEventArgs>(generator, "PageUpdated", siteGeneratorScheduler, (args) => HandleSiteUpdate(PageUpdated, args));
            EventHelper.SubscribeToWikiEvent<WikiSiteEventArgs>(generator, "PageDeleted", siteGeneratorScheduler, (args) => HandleSiteUpdate(PageDeleted, args));
            EventHelper.SubscribeToWikiEvent<WikiSiteEventArgs>(generator, "PageMoved", siteGeneratorScheduler, (args) => HandleSiteUpdate(PageMoved, args));

            // subscribe to initialisation event
            EventHelper.SubscribeToWikiEvent<EventSourceInitialisedArgs>(repository, "EventSourceInitialised", siteGeneratorScheduler, (e) => HandleRepositoryInitialised(e));

            // get the initial files
            IEnumerable<string> initialSourceFiles = Directory.EnumerateFiles(wikiConfig.RootSourcePath, wikiConfig.Convertor.FileSearchString, SearchOption.AllDirectories);

            repository.Init(
                watcher, 
                wikiConfig.RootSourcePath, 
                wikiConfig.RootWikiPath, 
                initialSourceFiles);
        }

        public void Dispose()
        {
            watcher.Dispose();
            repository.Dispose();

            watcher.Dispose();

            siteGeneratorScheduler.Dispose();
        }

        private void HandleRepositoryInitialised(EventSourceInitialisedArgs args)
        {
            watcher.Init();

            var handler = InitialisationComplete;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
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
