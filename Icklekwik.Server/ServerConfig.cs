using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Icklekwik.Core.Cache;
using Icklewik.Core;
using Icklewik.Core.Model;

namespace Icklekwik.Server
{
    /// <summary>
    /// Thread-safe class containing configuration for a number of Wikis
    /// </summary>
    public class ServerConfig
    {
        // TODO: The concurrent dictionary doesn't really help us here. The WikiConfig should never change
        // but the model will be updated continously and needs to be read by multiple threads - we need to
        // implement a proper thread safe way of reading the model details. Or else use some sort of caching layer
        // here that has threadsafety built in
        private ConcurrentDictionary<string, Tuple<WikiConfig, MasterRepository, IPageCache>> configMap;

        public ServerConfig(IEnumerable<Tuple<WikiConfig, IPageCache>> config)
        {
            configMap = new ConcurrentDictionary<string, Tuple<WikiConfig, MasterRepository, IPageCache>>(
                config.ToDictionary(
                    c => CreateSafeName(c.Item1.SiteName),
                    c => new Tuple<WikiConfig, MasterRepository, IPageCache>(c.Item1, new MasterRepository(c.Item1.Convertor.FileExtension), c.Item2)));
        }

        public IEnumerable<WikiConfig> AllConfig
        {
            get
            {
                return configMap.Values.Select(t => t.Item1);
            }
        }

        public bool TryGetConfig(string siteName, out WikiConfig value)
        {
            string safeName = CreateSafeName(siteName);

            Tuple<WikiConfig, MasterRepository, IPageCache> tuple;
            if (configMap.TryGetValue(siteName, out tuple))
            {
                value = tuple.Item1;
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }

        public bool TryGetMasterRepository(string siteName, out MasterRepository value)
        {
            string safeName = CreateSafeName(siteName);

            Tuple<WikiConfig, MasterRepository, IPageCache> tuple;
            if (configMap.TryGetValue(siteName, out tuple))
            {
                value = tuple.Item2;
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }

        public bool TryGetPageCache(string siteName, out IPageCache value)
        {
            string safeName = CreateSafeName(siteName);

            Tuple<WikiConfig, MasterRepository, IPageCache> tuple;
            if (configMap.TryGetValue(siteName, out tuple))
            {
                value = tuple.Item3;
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }

        private string CreateSafeName(string siteName)
        {
            return siteName.Replace(' ', '_');
        }
    }
}
