using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Icklewik.Core.Model
{
    public interface IWikiModelEventSource
    {
        event Action<object, WikiRepositoryEventArgs> PageAdded;
        event Action<object, WikiRepositoryEventArgs> PageUpdated;
        event Action<object, WikiRepositoryEventArgs> PageDeleted;
        event Action<object, WikiRepositoryEventArgs> PageMoved;

        event Action<object, WikiRepositoryEventArgs> DirectoryAdded;
        event Action<object, WikiRepositoryEventArgs> DirectoryUpdated;
        event Action<object, WikiRepositoryEventArgs> DirectoryDeleted;
        event Action<object, WikiRepositoryEventArgs> DirectoryMoved;
    }
}
