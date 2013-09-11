using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Icklewik.Core.Site
{
    public interface IWikiSiteEvents
    {
        event Action<object, WikiSiteEventArgs> PageAdded;
        event Action<object, WikiSiteEventArgs> PageUpdated;
        event Action<object, WikiSiteEventArgs> PageDeleted;
        event Action<object, WikiSiteEventArgs> PageMoved;

        event Action<object, WikiSiteEventArgs> DirectoryAdded;
        event Action<object, WikiSiteEventArgs> DirectoryUpdated;
        event Action<object, WikiSiteEventArgs> DirectoryDeleted;
        event Action<object, WikiSiteEventArgs> DirectoryMoved;
    }
}
