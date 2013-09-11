using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Threading;
using Icklekwik.Core.Cache;
using Icklekwik.Server;
using Icklewik.Core;
using Icklewik.Core.Model;
using Icklewik.Core.Site;
using Microsoft.AspNet.SignalR.Hosting.Self;
using Nancy.Hosting.Self;
using Icklewik.Core.Source;

namespace Icklewik.WindowsService
{
    public class ServerStartup : IDisposable
    {
        private ServerConfig serverConfig;
        private IDictionary<string, WikiSite> sites;

        private NancyHost nancyHost;
        private Server signalrHost;

        private IScheduler notificationScheduler;
        private IScheduler modelSyncScheduler;

        public ServerStartup(ServerConfig config, bool enableDiagnostics = false, string diagnosticsPassword = "")
        {
            serverConfig = config;
            sites = new Dictionary<string, WikiSite>();

            // TODO: Investigate event ordering if these two schedulers are in fact the same (slow handling seems to cause notifications out of order)
            notificationScheduler = new EventLoopScheduler(threadStart => new Thread(threadStart) { Name = "NotificationScheduler" });
            modelSyncScheduler = new EventLoopScheduler(threadStart => new Thread(threadStart) { Name = "ModelSyncScheduler" });

            foreach (var wikiConfig in config.AllConfig)
            {
                // get the relevant repositories
                MasterRepository masterRepository;
                IPageCache pageCache;
                if (serverConfig.TryGetMasterRepository(wikiConfig.SiteName, out masterRepository) &&
                    serverConfig.TryGetPageCache(wikiConfig.SiteName, out pageCache))
                {
                    var sourceWatcher = new SourceWatcher(wikiConfig.RootSourcePath, wikiConfig.Convertor.FileSearchString);

                    // create site
                    var site = new WikiSite(wikiConfig, masterRepository, sourceWatcher, pageCache);

                    // subscribe to all the sites events for notification purposes
                    EventHelper.SubscribeToWikiEvent<WikiSiteEventArgs>(site, "PageAdded", notificationScheduler, (args) => HandleEvent(site.Name, args.WikiUrl));
                    EventHelper.SubscribeToWikiEvent<WikiSiteEventArgs>(site, "PageUpdated", notificationScheduler, (args) => HandleEvent(site.Name, args.WikiUrl));
                    EventHelper.SubscribeToWikiEvent<WikiSiteEventArgs>(site, "PageDeleted", notificationScheduler, (args) => HandleEvent(site.Name, args.WikiUrl));
                    EventHelper.SubscribeToWikiEvent<WikiSiteEventArgs>(site, "PageMoved", notificationScheduler, (args) => HandleEvent(site.Name, args.WikiUrl));
                    EventHelper.SubscribeToWikiEvent<WikiSiteEventArgs>(site, "DirectoryAdded", notificationScheduler, (args) => HandleEvent(site.Name, args.WikiUrl));
                    EventHelper.SubscribeToWikiEvent<WikiSiteEventArgs>(site, "DirectoryUpdated", notificationScheduler, (args) => HandleEvent(site.Name, args.WikiUrl));
                    EventHelper.SubscribeToWikiEvent<WikiSiteEventArgs>(site, "DirectoryDeleted", notificationScheduler, (args) => HandleEvent(site.Name, args.WikiUrl));
                    EventHelper.SubscribeToWikiEvent<WikiSiteEventArgs>(site, "DirectoryMoved", notificationScheduler, (args) => HandleEvent(site.Name, args.WikiUrl));

                    // store
                    sites[wikiConfig.SiteName] = site;
                }
            }

            WikiBootstrapper bootstrapper = new WikiBootstrapper(config, enableDiagnostics, diagnosticsPassword);

            nancyHost = new NancyHost(new Uri("http://localhost:8070/"), bootstrapper);
            signalrHost = new Server("http://localhost:8071/");
        }

        public void Start()
        {
            foreach (var site in sites.Values)
            {
                site.Start();
            }

            nancyHost.Start();

            // Map the default hub url (/signalr)
            signalrHost.MapHubs();

            signalrHost.Start();
        }

        public void Dispose()
        {
            nancyHost.Stop();

            signalrHost.Stop();
            signalrHost.Dispose();

            foreach (var site in sites.Values)
            {
                // update the site's copy of the model

                site.PageAdded -= (sender, args) => HandleEvent(site.Name, args.WikiUrl);
                site.PageUpdated -= (sender, args) => HandleEvent(site.Name, args.WikiUrl);
                site.PageDeleted -= (sender, args) => HandleEvent(site.Name, args.WikiUrl);
                site.PageMoved -= (sender, args) => HandleEvent(site.Name, args.WikiUrl);
                site.DirectoryAdded -= (sender, args) => HandleEvent(site.Name, args.WikiUrl);
                site.DirectoryUpdated -= (sender, args) => HandleEvent(site.Name, args.WikiUrl);
                site.DirectoryDeleted -= (sender, args) => HandleEvent(site.Name, args.WikiUrl);
                site.DirectoryMoved -= (sender, args) => HandleEvent(site.Name, args.WikiUrl);

                site.Dispose();
            }

            sites = null;
        }

        private void HandleEvent(string siteName, string wikiUrl)
        {
            // notify any interested parties
            ChangeNotifier.NotifyPageUpdated(siteName, wikiUrl);
        }
    }
}
