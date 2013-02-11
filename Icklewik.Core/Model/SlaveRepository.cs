using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text;

namespace Icklewik.Core.Model
{
    /// <summary>
    /// This is a read-only repository that is kept updated by it's subscription to an 
    /// event source.
    /// 
    /// TODO: Two repository types seems unnecessary. Would be better to update the MasterWiki
    /// to allow threadsafe read-only access via an async interface
    /// </summary>
    public class SlaveRepository : WikiRepository<NullLockPolicy>
    {
        public SlaveRepository(string fileExtension)
            : base(fileExtension)
        {
        }

        public WikiModel Model
        {
            get
            {
                return UnderlyingModel;
            }
        }

        public void Init(IWikiModelEventSource eventSource, IScheduler scheduler)
        {
            // subscripe to the event source and make sure our local model is updated whenever
            // an event occurs
            EventHelper.SubscribeToEvent<WikiRepositoryEventArgs>(eventSource, "PageAdded", scheduler, (args) => AddPage(args.SourcePath), (args) => args.SourcePath);
            EventHelper.SubscribeToEvent<WikiRepositoryEventArgs>(eventSource, "PageUpdated", scheduler, (args) => UpdatePage(args.SourcePath), (args) => args.SourcePath);
            EventHelper.SubscribeToEvent<WikiRepositoryEventArgs>(eventSource, "PageDeleted", scheduler, (args) => DeletePage(args.SourcePath), (args) => args.SourcePath);
            EventHelper.SubscribeToEvent<WikiRepositoryEventArgs>(eventSource, "PageMoved", scheduler, (args) => RenamePage(args.OldSourcePath, args.SourcePath), (args) => args.SourcePath);
            EventHelper.SubscribeToEvent<WikiRepositoryEventArgs>(eventSource, "DirectoryAdded", scheduler, (args) => AddDirectory(args.SourcePath), (args) => args.SourcePath);
            EventHelper.SubscribeToEvent<WikiRepositoryEventArgs>(eventSource, "DirectoryUpdated", scheduler, (args) => UpdateDirectory(args.SourcePath), (args) => args.SourcePath);
            EventHelper.SubscribeToEvent<WikiRepositoryEventArgs>(eventSource, "DirectoryDeleted", scheduler, (args) => DeleteDirectory(args.SourcePath), (args) => args.SourcePath);
            EventHelper.SubscribeToEvent<WikiRepositoryEventArgs>(eventSource, "DirectoryMoved", scheduler, (args) => RenameDirectory(args.OldSourcePath, args.SourcePath), (args) => args.SourcePath);

            eventSource.EventSourceStarted += () =>
                {
                    // assume the root paths has already been set
                    AddDirectory(PathHelper.GetFullPath(eventSource.RootSourcePath), new RootInfo { RootWikiPath = PathHelper.GetFullPath(eventSource.RootWikiPath) });
                };
        }

        protected override void HandlePageAdded(WikiPage page)
        {
        }

        protected override void HandlePageUpdated(WikiPage page)
        {
        }

        protected override void HandlePageDeleted(WikiPage page)
        {
        }

        protected override void HandlePageMoved(WikiPage page, string oldSourcePath, string oldWikiPath)
        {
        }

        protected override void HandleDirectoryAdded(WikiDirectory page)
        {
        }

        protected override void HandleDirectoryUpdated(WikiDirectory page)
        {
        }

        protected override void HandleDirectoryDeleted(WikiDirectory page)
        {
        }

        protected override void HandleDirectoryMoved(WikiDirectory page, string oldSourcePath, string oldWikiPath)
        {
        }
    }
}
