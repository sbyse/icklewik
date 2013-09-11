using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Icklewik.Core.Model;

namespace Icklekwik.Core.Cache
{
    /// <summary>
    /// Allows consumers access to a cache frequently accessed
    /// </summary>
    public interface IPageCache
    {
        // TODO: Async-ify
        bool TryGetContents(string wikiUrl, out string content);
        void CachePageContents(string wikiUrl, string content);
        void DeleteFromCache(string wikiUrl);
    }
}
