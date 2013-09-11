using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Icklewik.Core.Site;

namespace Icklewik.Core.Source
{
    public interface IWikiSourceEvents
    {
        event Action<object, WikiSourceEventArgs> FileAdded;
        event Action<object, WikiSourceEventArgs> FileUpdated;
        event Action<object, WikiSourceEventArgs> FileDeleted;
        event Action<object, WikiSourceEventArgs> FileMoved;

        event Action<object, WikiSourceEventArgs> DirectoryAdded;
        event Action<object, WikiSourceEventArgs> DirectoryUpdated;
        event Action<object, WikiSourceEventArgs> DirectoryDeleted;
        event Action<object, WikiSourceEventArgs> DirectoryMoved;
    }
}
