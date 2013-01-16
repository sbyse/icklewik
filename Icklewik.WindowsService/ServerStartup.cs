using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Icklekwik.Server;
using Icklewik.Core;
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

        public ServerStartup(ServerConfig config, bool enableDiagnostics = false, string diagnosticsPassword = "")
        {
            serverConfig = config;
            sites = new Dictionary<string, WikiSite>();

            foreach (var wikiConfig in config.AllConfig)
            {
                // create site
                var site = new WikiSite(wikiConfig);

                // subscribe to all events
                site.PageAdded += (sender, args) => HandleEvent(site.Name, args.WikiUrl);
                site.PageUpdated += (sender, args) => HandleEvent(site.Name, args.WikiUrl);
                site.PageDeleted += (sender, args) => HandleEvent(site.Name, args.WikiUrl);
                site.PageMoved += (sender, args) => HandleEvent(site.Name, args.WikiUrl);
                site.DirectoryAdded += (sender, args) => HandleEvent(site.Name, args.WikiUrl);
                site.DirectoryUpdated += (sender, args) => HandleEvent(site.Name, args.WikiUrl);
                site.DirectoryDeleted += (sender, args) => HandleEvent(site.Name, args.WikiUrl);
                site.DirectoryMoved += (sender, args) => HandleEvent(site.Name, args.WikiUrl);

                // store
                sites[wikiConfig.SiteName] = site;
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
            // update the site's copy of the model
            WikiModel localModel;
            if (serverConfig.TryGetModel(siteName, out localModel)
            {
                // TODO: Update model (need more details)
            }

            // notify any interested parties
            ChangeNotifier.NotifyPageUpdated(siteName, wikiUrl);
        }
    }
}
