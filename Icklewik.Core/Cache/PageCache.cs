using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Icklewik.Core.Model;

namespace Icklekwik.Core.Cache
{
    public class PageCache : IPageCache
    {
        private const int MaxCacheSize = 100;

        // maps wiki url to contents
        private IDictionary<string, string> cache;

        // queue of cached urls
        private Queue<string> fifoQueue;

        private object locker = new object();

        public PageCache()
        {
            // TODO: This is a v poor implementation, needs sorting out
            cache = new Dictionary<string, string>();
            fifoQueue = new Queue<string>(MaxCacheSize);
        }

        public bool TryGetContents(string wikiUrl, out string content)
        {
            lock (locker)
            {
                return cache.TryGetValue(wikiUrl, out content);
            }
        }

        public void CachePageContents(string wikiUrl, string content)
        {
            lock (locker)
            {
                //  if necessary remove old item from the queue
                if (fifoQueue.Count() == MaxCacheSize)
                {
                    string oldestUrl = fifoQueue.Dequeue();
                    cache.Remove(oldestUrl);
                }

                fifoQueue.Enqueue(wikiUrl);

                cache[wikiUrl] = content;
            }
        }

        public void DeleteFromCache(string wikiUrl)
        {
            lock (locker)
            {
                // TODO: Need to fix queueing mechanism
                
                cache.Remove(wikiUrl);
            }
        }
    }
}
