using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Threading;

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
            EventHelper.SubscribeToWikiEvent<WikiModelEventArgs>(eventSource, "PageAdded", scheduler, (args) => AddPage(args.SourcePath));
            EventHelper.SubscribeToWikiEvent<WikiModelEventArgs>(eventSource, "PageUpdated", scheduler, (args) => UpdatePage(args.SourcePath));
            EventHelper.SubscribeToWikiEvent<WikiModelEventArgs>(eventSource, "PageDeleted", scheduler, (args) => DeletePage(args.SourcePath));
            EventHelper.SubscribeToWikiEvent<WikiModelEventArgs>(eventSource, "PageMoved", scheduler, (args) => RenamePage(args.OldSourcePath, args.SourcePath));
            EventHelper.SubscribeToWikiEvent<WikiModelEventArgs>(eventSource, "DirectoryAdded", scheduler, (args) => AddDirectory(args.SourcePath));
            EventHelper.SubscribeToWikiEvent<WikiModelEventArgs>(eventSource, "DirectoryUpdated", scheduler, (args) => UpdateDirectory(args.SourcePath));
            EventHelper.SubscribeToWikiEvent<WikiModelEventArgs>(eventSource, "DirectoryDeleted", scheduler, (args) => DeleteDirectory(args.SourcePath));
            EventHelper.SubscribeToWikiEvent<WikiModelEventArgs>(eventSource, "DirectoryMoved", scheduler, (args) => RenameDirectory(args.OldSourcePath, args.SourcePath));

            eventSource.EventSourceStarted += (target, args) =>
                {
                    scheduler.Schedule(() =>
                        {
                            // assume the root paths has already been set
                            AddDirectory(PathHelper.GetFullPath(eventSource.RootSourcePath), new RootInfo { RootWikiPath = PathHelper.GetFullPath(eventSource.RootWikiPath) });
                        });
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
