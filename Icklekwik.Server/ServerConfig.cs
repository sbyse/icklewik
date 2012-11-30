using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Icklewik.Core;

namespace Icklekwik.Server
{
    /// <summary>
    /// Thread-safe class containing configuration for a number of Wikis
    /// </summary>
    public class ServerConfig
    {
        private ConcurrentDictionary<string, WikiConfig> configMap;

        public ServerConfig(IEnumerable<WikiConfig> config)
        {
            configMap = new ConcurrentDictionary<string, WikiConfig>(config.ToDictionary(c => CreateSafeName(c.SiteName)));
        }

        public IEnumerable<WikiConfig> AllConfig
        {
            get
            {
                return configMap.Values;
            }
        }

        public bool TryGetConfig(string siteName, out WikiConfig value)
        {
            string safeName = CreateSafeName(siteName);

            return configMap.TryGetValue(siteName, out value);
        }

        private string CreateSafeName(string siteName)
        {
            return siteName.Replace(' ', '_');
        }
    }
}
