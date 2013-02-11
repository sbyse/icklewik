using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Icklewik.Core.Model
{
    public interface IWikiModelEventSource
    {
        event Action EventSourceStarted;

        event Action<object, WikiRepositoryEventArgs> PageAdded;
        event Action<object, WikiRepositoryEventArgs> PageUpdated;
        event Action<object, WikiRepositoryEventArgs> PageDeleted;
        event Action<object, WikiRepositoryEventArgs> PageMoved;

        event Action<object, WikiRepositoryEventArgs> DirectoryAdded;
        event Action<object, WikiRepositoryEventArgs> DirectoryUpdated;
        event Action<object, WikiRepositoryEventArgs> DirectoryDeleted;
        event Action<object, WikiRepositoryEventArgs> DirectoryMoved;

        /// <summary>
        /// Represents the file system location at the top of the source tree
        /// </summary>
        string RootSourcePath { get; }

        /// <summary>
        /// Represents the file system location that holds the generated wiki
        /// files. The files in this location should be treated as temporary and will
        /// be regenerated in response to changes in the root source path
        /// </summary>
        string RootWikiPath { get; }
    }
}
