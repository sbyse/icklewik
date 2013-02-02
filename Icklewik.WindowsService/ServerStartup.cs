using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading.Tasks;
using Icklekwik.Server;
using Icklewik.Core;
using Icklewik.Core.Model;
using Microsoft.AspNet.SignalR.Hosting.Self;
using Nancy.Hosting.Self;

namespace Icklewik.WindowsService
{
    public class ServerStartup : IDisposable
    {
        private ServerConfig serverConfig;
        private IDictionary<string, WikiSite> sites;

        private NancyHost nancyHost;
        private Server signalrHost;

        private IScheduler notificationScheduler;

        public ServerStartup(ServerConfig config, bool enableDiagnostics = false, string diagnosticsPassword = "")
        {
            serverConfig = config;
            sites = new Dictionary<string, WikiSite>();

            notificationScheduler = Scheduler.Default; 

            foreach (var wikiConfig in config.AllConfig)
            {
                // create site
                var site = new WikiSite(wikiConfig);

                // subscribe to all the sites events for notification purposes
                EventHelper.SubscribeToEvent<WikiRepositoryEventArgs>(site, "PageAdded", notificationScheduler, (args) => HandleEvent(site.Name, args.WikiUrl), (args) => args.SourcePath);
                EventHelper.SubscribeToEvent<WikiRepositoryEventArgs>(site, "PageUpdated", notificationScheduler, (args) => HandleEvent(site.Name, args.WikiUrl), (args) => args.SourcePath);
                EventHelper.SubscribeToEvent<WikiRepositoryEventArgs>(site, "PageDeleted", notificationScheduler, (args) => HandleEvent(site.Name, args.WikiUrl), (args) => args.SourcePath);
                EventHelper.SubscribeToEvent<WikiRepositoryEventArgs>(site, "PageMoved", notificationScheduler, (args) => HandleEvent(site.Name, args.WikiUrl), (args) => args.SourcePath);
                EventHelper.SubscribeToEvent<WikiRepositoryEventArgs>(site, "DirectoryAdded", notificationScheduler, (args) => HandleEvent(site.Name, args.WikiUrl), (args) => args.SourcePath);
                EventHelper.SubscribeToEvent<WikiRepositoryEventArgs>(site, "DirectoryUpdated", notificationScheduler, (args) => HandleEvent(site.Name, args.WikiUrl), (args) => args.SourcePath);
                EventHelper.SubscribeToEvent<WikiRepositoryEventArgs>(site, "DirectoryDeleted", notificationScheduler, (args) => HandleEvent(site.Name, args.WikiUrl), (args) => args.SourcePath);
                EventHelper.SubscribeToEvent<WikiRepositoryEventArgs>(site, "DirectoryMoved", notificationScheduler, (args) => HandleEvent(site.Name, args.WikiUrl), (args) => args.SourcePath);

                // store
                sites[wikiConfig.SiteName] = site;

                // make sure the slave repository keeps in sync

                // update the site's copy of the model
                SlaveRepository slaveRepository;
                if (serverConfig.TryGetRepository(wikiConfig.SiteName, out slaveRepository))
                {
                    // the slave repository is currently updated by a thread from the pool
                    slaveRepository.Init(site, notificationScheduler);
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
