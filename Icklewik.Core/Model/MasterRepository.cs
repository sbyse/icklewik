using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Icklewik.Core.Model
{
    /// <summary>
    /// Holds the canonical representation of a wiki.
    /// 
    /// Changes to the repository are broadcast by firing the relevant events
    /// </summary>
    public class MasterRepository : WikiRepository<NullLockPolicy>, IWikiModelEventSource
    {
        public MasterRepository(string fileExtension)
            : base(fileExtension)
        {
        }

        public event Action EventSourceStarted;

        public event Action<object, WikiRepositoryEventArgs> PageAdded;
        public event Action<object, WikiRepositoryEventArgs> PageUpdated;
        public event Action<object, WikiRepositoryEventArgs> PageDeleted;
        public event Action<object, WikiRepositoryEventArgs> PageMoved;

        public event Action<object, WikiRepositoryEventArgs> DirectoryAdded;
        public event Action<object, WikiRepositoryEventArgs> DirectoryUpdated;
        public event Action<object, WikiRepositoryEventArgs> DirectoryDeleted;
        public event Action<object, WikiRepositoryEventArgs> DirectoryMoved;

        public void Init(string rootSourcePath, string rootWikiPath, IEnumerable<string> initialFiles)
        {
            // create the root directory as as special case
            AddDirectory(PathHelper.GetFullPath(rootSourcePath), new RootInfo { RootWikiPath = PathHelper.GetFullPath(rootWikiPath) });

            // iterate over all directories and add all relevant files to the model (which will fire the relevant events and prompt the
            // generator to generate the wiki itself)
            foreach (string markdownFilePath in initialFiles.Select(path => PathHelper.GetFullPath(path)))
            {
                if (!UnderlyingModel.PathComparer.Equals(markdownFilePath, UnderlyingModel.RootSourcePath))
                {
                    AddPage(markdownFilePath);
                }
            }

            if (EventSourceStarted != null)
            {
                EventSourceStarted();
            }
        }

        protected override void HandlePageAdded(WikiPage page)
        {
            FireEvent(PageAdded, page);
        }

        protected override void HandlePageUpdated(WikiPage page)
        {
            FireEvent(PageUpdated, page);
        }

        protected override void HandlePageDeleted(WikiPage page)
        {
            FireEvent(PageDeleted, page);
        }

        protected override void HandlePageMoved(WikiPage page, string oldSourcePath, string oldWikiPath)
        {
            FireEvent(PageMoved, page, oldSourcePath, oldWikiPath);
        }

        protected override void HandleDirectoryAdded(WikiDirectory page)
        {
            FireEvent(DirectoryAdded, page);
        }

        protected override void HandleDirectoryUpdated(WikiDirectory page)
        {
            FireEvent(DirectoryUpdated, page);
        }

        protected override void HandleDirectoryDeleted(WikiDirectory page)
        {
            FireEvent(DirectoryDeleted, page);
        }

        protected override void HandleDirectoryMoved(WikiDirectory page, string oldSourcePath, string oldWikiPath)
        {
            FireEvent(DirectoryMoved, page, oldSourcePath, oldWikiPath);
        }

        private void FireEvent(Action<object, WikiRepositoryEventArgs> eventToFire, WikiEntry entry, string oldSourcePath = "", string oldWikiPath = "")
        {
            if (eventToFire != null)
            {
                eventToFire(this, new WikiRepositoryEventArgs(
                    sourcePath: entry.SourcePath,
                    wikiPath: entry.WikiPath,
                    wikiUrl: entry.WikiUrl,
                    oldSourcePath: oldSourcePath,
                    oldWikiPath: oldWikiPath));
            }
        }
    }

}
