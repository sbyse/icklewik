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
        // TODO: The concurrent dictionary doesn't really help us here. The WikiConfig should never change
        // but the model will be updated continously and needs to be read by multiple threads - we need to
        // implement a proper thread safe way of reading the model details. Or else use some sort of caching layer
        // here that has threadsafey built in
        private ConcurrentDictionary<string, Tuple<WikiConfig, WikiModel>> configMap;

        public ServerConfig(IEnumerable<WikiConfig> config)
        {
            configMap = new ConcurrentDictionary<string, Tuple<WikiConfig, WikiModel>>(
                config.ToDictionary(
                    c => CreateSafeName(c.SiteName),
                    c => new Tuple<WikiConfig, WikiModel>(c, new WikiModel())));
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

            Tuple<WikiConfig, WikiModel> tuple;
            if (configMap.TryGetValue(siteName, out tuple))
            {
                value = tuple.Item1;
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool TryGetModel(string siteName, out WikiModel value)
        {
            string safeName = CreateSafeName(siteName);

            Tuple<WikiConfig, WikiModel> tuple;
            if (configMap.TryGetValue(siteName, out tuple))
            {
                value = tuple.Item2;
                return true;
            }
            else
            {
                return false;
            }
        }

        private string CreateSafeName(string siteName)
        {
            return siteName.Replace(' ', '_');
        }
    }
}
