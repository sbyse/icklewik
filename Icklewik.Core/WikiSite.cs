using System.IO;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using System.Reactive;
using System.Threading;

namespace Icklewik.Core
{
    public class WikiSite : IDisposable
    {
        private WikiModel model;
        private WikiGenerator generator;
        private FileSystemWatcher fileWatcher;
        private FileSystemWatcher directoryWatcher;
        private EventLoopScheduler scheduler;

        public WikiSite(string rootSourcePath, string rootWikiPath, Convertor sourceConvertor)
        {
            model = new WikiModel(sourceConvertor.FileExtension);

            generator = new WikiGenerator(rootSourcePath, rootWikiPath, sourceConvertor);

            model.DirectoryAdded += (directory) => generator.CreateDirectory(directory);
            model.DirectoryUpdated += (directory) => generator.UpdateDirectory(directory);
            model.DirectoryDeleted += (directory) => generator.DeleteDirectory(directory);

            model.PageAdded += (page) => generator.CreatePage(page);
            model.PageUpdated += (page) => generator.UpdatePage(page);
            model.PageDeleted += (page) => generator.DeletePage(page);

            // initialise the model
            model.Init(rootSourcePath, rootWikiPath, Directory.EnumerateFiles(rootSourcePath, sourceConvertor.FileSearchString, SearchOption.AllDirectories));

            // now we create a file system watcher to make sure we stay in sync with future changes
            fileWatcher = new FileSystemWatcher();
            fileWatcher.Path = rootSourcePath;
            fileWatcher.Filter = sourceConvertor.FileSearchString;
            fileWatcher.IncludeSubdirectories = true;

            fileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;

            directoryWatcher = new FileSystemWatcher();
            directoryWatcher.Path = rootSourcePath;
            directoryWatcher.IncludeSubdirectories = true;
            directoryWatcher.NotifyFilter = NotifyFilters.DirectoryName;

            scheduler = new EventLoopScheduler();
        }

        public void Start()
        {
            System.Console.WriteLine(string.Format("Subscribing on thread: {0}", Thread.CurrentThread.ManagedThreadId));

            SubscribeToEvent<FileSystemEventArgs>(fileWatcher, "Created", (e) => model.AddPage(e.FullPath));
            SubscribeToEvent<FileSystemEventArgs>(fileWatcher, "Changed", (e) => model.UpdatePage(e.FullPath));
            SubscribeToEvent<FileSystemEventArgs>(fileWatcher, "Deleted", (e) => model.DeletePage(e.FullPath));
            SubscribeToEvent<RenamedEventArgs>(fileWatcher, "Renamed", (e) => model.RenamePage(e.OldFullPath, e.FullPath));

            SubscribeToEvent<FileSystemEventArgs>(directoryWatcher, "Created", (e) => model.AddDirectory(e.FullPath));
            SubscribeToEvent<FileSystemEventArgs>(directoryWatcher, "Changed", (e) => model.UpdateDirectory(e.FullPath));
            SubscribeToEvent<FileSystemEventArgs>(directoryWatcher, "Deleted", (e) => model.DeleteDirectory(e.FullPath));
            SubscribeToEvent<RenamedEventArgs>(directoryWatcher, "Renamed", (e) => model.RenameDirectory(e.OldFullPath, e.FullPath));

            // Begin watching the file system
            fileWatcher.EnableRaisingEvents = true;
            directoryWatcher.EnableRaisingEvents = true;
        }

        public void Dispose()
        {
            scheduler.Dispose();

            fileWatcher.EnableRaisingEvents = false;
            directoryWatcher.EnableRaisingEvents = false;

            fileWatcher.Dispose();
            directoryWatcher.Dispose();
        }

        private void SubscribeToEvent<TEventArgs>(FileSystemWatcher watcher, string eventName, Action<TEventArgs> eventAction) where TEventArgs : EventArgs
        {
            Observable.FromEventPattern<TEventArgs>(watcher, eventName)
                .ObserveOn(scheduler)
                .Subscribe(evt =>
                {
                    System.Console.WriteLine("Handling event ({0}) on thread: {1}", eventName, Thread.CurrentThread.ManagedThreadId);
                    eventAction(evt.EventArgs);
                });
        }
    }
}
