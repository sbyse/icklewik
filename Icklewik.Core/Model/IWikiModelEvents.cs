using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Icklewik.Core.Model
{
    public interface IWikiModelEvents
    {
        event Action<object, EventSourceInitialisedArgs> EventSourceInitialised;

        event Action<object, WikiModelEventArgs> PageAdded;
        event Action<object, WikiModelEventArgs> PageUpdated;
        event Action<object, WikiModelEventArgs> PageDeleted;
        event Action<object, WikiModelEventArgs> PageMoved;

        event Action<object, WikiModelEventArgs> DirectoryAdded;
        event Action<object, WikiModelEventArgs> DirectoryUpdated;
        event Action<object, WikiModelEventArgs> DirectoryDeleted;
        event Action<object, WikiModelEventArgs> DirectoryMoved;

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
