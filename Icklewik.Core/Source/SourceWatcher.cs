using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Icklewik.Core.Site;
using Icklewik.Core.Logging;

namespace Icklewik.Core.Source
{
    public class SourceWatcher : ISourceWatcher
    {
        private FileSystemWatcher fileWatcher;
        private FileSystemWatcher directoryWatcher;

        public SourceWatcher(string rootSourcePath, string fileSearchString)
        {
            // now we create a file system watcher to make sure we stay in sync with future changes
            fileWatcher = new FileSystemWatcher();
            fileWatcher.Path = rootSourcePath;
            fileWatcher.Filter = fileSearchString;
            fileWatcher.IncludeSubdirectories = true;
            fileWatcher.InternalBufferSize = 32768;

            fileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;

            directoryWatcher = new FileSystemWatcher();
            directoryWatcher.Path = rootSourcePath;
            directoryWatcher.IncludeSubdirectories = true;
            directoryWatcher.NotifyFilter = NotifyFilters.DirectoryName;
            directoryWatcher.InternalBufferSize = 32768;
        }

        public event Action<object, WikiSourceEventArgs> FileAdded;
        public event Action<object, WikiSourceEventArgs> FileUpdated;
        public event Action<object, WikiSourceEventArgs> FileDeleted;
        public event Action<object, WikiSourceEventArgs> FileMoved;

        public event Action<object, WikiSourceEventArgs> DirectoryAdded;
        public event Action<object, WikiSourceEventArgs> DirectoryUpdated;
        public event Action<object, WikiSourceEventArgs> DirectoryDeleted;
        public event Action<object, WikiSourceEventArgs> DirectoryMoved;
        public void Init()
        {
            // forward all events
            fileWatcher.Created += (sender, args) => FireEvent(FileAdded, args.FullPath);
            fileWatcher.Changed += (sender, args) => FireEvent(FileUpdated, args.FullPath);
            fileWatcher.Deleted += (sender, args) => FireEvent(FileDeleted, args.FullPath);
            fileWatcher.Renamed += (sender, args) => FireEvent(FileMoved, args.FullPath, args.OldFullPath);

            directoryWatcher.Created += (sender, args) => FireEvent(DirectoryAdded, args.FullPath);
            directoryWatcher.Changed += (sender, args) => FireEvent(DirectoryUpdated, args.FullPath);
            directoryWatcher.Deleted += (sender, args) => FireEvent(DirectoryDeleted, args.FullPath);
            directoryWatcher.Renamed += (sender, args) => FireEvent(DirectoryMoved, args.FullPath, args.OldFullPath);

            // Begin watching the file system
            fileWatcher.EnableRaisingEvents = true;
            directoryWatcher.EnableRaisingEvents = true;
        }

        public void Dispose()
        {
            fileWatcher.EnableRaisingEvents = false;
            directoryWatcher.EnableRaisingEvents = false;

            fileWatcher.Dispose();
            directoryWatcher.Dispose();
        }

        private void FireEvent(Action<object, WikiSourceEventArgs> eventToFire, string fullPath, string oldFullPath = "")
        {
            if (eventToFire != null)
            {
                WikiSourceEventArgs sourceEventArgs = new WikiSourceEventArgs(
                    fullPath,
                    oldFullPath);

                Log.Instance.Log(string.Format("Firing source event {1} -> {0}", fullPath, oldFullPath));

                eventToFire(this, sourceEventArgs);
            }
        }
    }
}
