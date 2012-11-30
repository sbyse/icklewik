using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;

namespace Icklekwik.Server
{
    public static class ChangeNotifier
    {
        public static void NotifyPageUpdated(string siteName, string wikiUrl)
        {
            GlobalHost.ConnectionManager.GetHubContext<WikiHub>().Clients.All.PageUpdated(siteName, wikiUrl);
        }
    }
}
