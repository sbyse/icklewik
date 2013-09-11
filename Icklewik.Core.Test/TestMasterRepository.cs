using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Icklewik.Core.Model;
using Icklewik.Core.Source;
using Xunit;

namespace Icklewik.Core.Test
{
    public class TestSourceWatcher : IWikiSourceEvents
    {
        public event System.Action<object, WikiSourceEventArgs> FileAdded;
        public event System.Action<object, WikiSourceEventArgs> FileUpdated;
        public event System.Action<object, WikiSourceEventArgs> FileDeleted;
        public event System.Action<object, WikiSourceEventArgs> FileMoved;

        public event System.Action<object, WikiSourceEventArgs> DirectoryAdded;
        public event System.Action<object, WikiSourceEventArgs> DirectoryUpdated;
        public event System.Action<object, WikiSourceEventArgs> DirectoryDeleted;
        public event System.Action<object, WikiSourceEventArgs> DirectoryMoved;

        public void FireFileAdded(string fullPath)
        {
            FireEvent(FileAdded, fullPath);
        }

        public void FireFileUpdated(string fullPath)
        {
            FireEvent(FileUpdated, fullPath);
        }

        public void FireFileDeleted(string fullPath)
        {
            FireEvent(FileDeleted, fullPath);
        }

        public void FireFileMoved(string fullPath, string oldFullPath)
        {
            FireEvent(FileMoved, fullPath, oldFullPath);
        }

        public void FireDirectoryAdded(string fullPath)
        {
            FireEvent(DirectoryAdded, fullPath);
        }

        public void FireDirectoryUpdated(string fullPath)
        {
            FireEvent(DirectoryUpdated, fullPath);
        }

        public void FireDirectoryDeleted(string fullPath)
        {
            FireEvent(DirectoryDeleted, fullPath);
        }

        public void FireDirectoryMoved(string fullPath, string oldFullPath)
        {
            FireEvent(DirectoryMoved, fullPath, oldFullPath);
        }

        private void FireEvent(Action<object, WikiSourceEventArgs> eventToFire, string fullPath, string oldFullPath = "")
        {
            if (eventToFire != null)
            {
                eventToFire(this, new WikiSourceEventArgs(
                    sourcePath: fullPath,
                    oldSourcePath: oldFullPath));
            }
        }
    }

    /// <summary>
    /// Tests the master repository without actually needing to add files to the file system
    /// 
    /// MasterRepository does not provide access to the internal model but we can test performance
    /// by observing the emitted events
    /// </summary>
    public class TestMasterRepository
    {
        private IList<string> createdDirectories;
        private IList<string> updatedDirectories;
        private IList<string> deletedDirectories;
        private IList<string> movedDirectories;

        private IList<string> createdPages;
        private IList<string> updatedPages;
        private IList<string> deletedPages;
        private IList<string> movedPages;

        private TestSourceWatcher watcher;
        private MasterRepository repository;

        public TestMasterRepository()
        {
            createdDirectories = new List<string>();
            updatedDirectories = new List<string>();
            deletedDirectories = new List<string>();
            movedDirectories = new List<string>();

            createdPages = new List<string>();
            updatedPages = new List<string>();
            deletedPages = new List<string>();
            movedPages = new List<string>();

            watcher = new TestSourceWatcher();
            repository = new MasterRepository(".md");

            // setup event handlers
            repository.DirectoryAdded += (source, args) =>
                {
                    createdDirectories.Add(args.SourcePath);
                };
            repository.DirectoryUpdated += (source, args) =>
                {
                    updatedDirectories.Add(args.SourcePath);
                };
            repository.DirectoryDeleted += (source, args) =>
                {
                    deletedDirectories.Add(args.SourcePath);
                };
            repository.DirectoryMoved += (source, args) => movedDirectories.Add(args.SourcePath);

            repository.PageAdded += (source, args) =>
            {
                createdPages.Add(args.SourcePath);
            };

            repository.PageUpdated += (source, args) =>
            {
                updatedPages.Add(args.SourcePath);
            };

            repository.PageDeleted += (source, args) =>
            {
                deletedPages.Add(args.SourcePath);
            };

            repository.PageMoved += (source, args) => 
            {
                movedPages.Add(args.SourcePath);
            };


            // create list of files
            IList<string> markdownFiles = new List<string>
                {
                    Path.Combine(".", "index.md"),
                    Path.Combine(".", "subdir1", "subdir2", "index.md")
                };

            // initialise the model (should fire events the same as addition)
            repository.Init(watcher, ".", Path.Combine(".", "somewikipath"), markdownFiles);
        }

        [Fact]
        public void ModelInitialisationFiresEvents()
        {
            // check that the directory created events fired
            Assert.Equal(3, createdDirectories.Count());
            Assert.Equal(0, updatedDirectories.Count());
            Assert.Equal(0, deletedDirectories.Count());
            Assert.Equal(0, movedDirectories.Count());

            Assert.Equal(2, createdPages.Count());
            Assert.Equal(0, updatedPages.Count());
            Assert.Equal(0, movedPages.Count());
        }

        [Fact]
        public void ModelFiresPageCreatedEvent()
        {
            int createdDirectoryCount = createdDirectories.Count();
            int createdPagesCount = createdPages.Count();

            watcher.FireFileAdded(Path.Combine(".", "subdir1", "index.md"));
            watcher.FireFileAdded(Path.Combine(".", "firstFile.md"));

            // make sure the event gets a chance to be noticed
            System.Threading.Thread.Sleep(10);

            // no more directories created
            Assert.Equal(createdDirectoryCount, createdDirectories.Count());

            // 2 more page created
            Assert.Equal(createdPagesCount + 2, createdPages.Count());
        }

        [Fact]
        public void ModelFiresPageUpdatedEvent()
        {
            int updatedDirectoryCount = updatedDirectories.Count();
            int updatedPagesCount = updatedPages.Count();

            watcher.FireFileUpdated(Path.Combine(".", "subdir1", "subdir2", "index.md"));
            watcher.FireFileUpdated(Path.Combine(".", "index.md"));

            // make sure the event gets a chance to be noticed
            System.Threading.Thread.Sleep(10);

            // no more directories updated
            Assert.Equal(updatedDirectoryCount, updatedDirectories.Count());

            // 2 more pages updated
            Assert.Equal(updatedPagesCount + 2, updatedPages.Count());
        }

        [Fact]
        public void ModelFiresPageDeletedEvent()
        {
            int deletedDirectoryCount = deletedDirectories.Count();
            int deletedPagesCount = deletedPages.Count();

            watcher.FireFileDeleted(Path.Combine(".", "index.md"));

            // make sure the event gets a chance to be noticed
            System.Threading.Thread.Sleep(10);

            // no more directories deleted
            Assert.Equal(deletedDirectoryCount, deletedDirectories.Count());

            // 1 more pages deleted
            Assert.Equal(deletedPagesCount + 1, deletedPages.Count());
        }

        [Fact]
        public void ModelFiresPageDeletedAndDirectoryDeletedEvent()
        {
            int deletedDirectoryCount = deletedDirectories.Count();
            int deletedPagesCount = deletedPages.Count();

            watcher.FireFileDeleted(Path.Combine(".", "subdir1", "subdir2", "index.md"));

            // make sure the event gets a chance to be noticed
            System.Threading.Thread.Sleep(10);

            // 2 more directories deleted (subdir1 and subdir2)
            Assert.Equal(deletedDirectoryCount + 2, deletedDirectories.Count());

            // 1 more pages deleted
            Assert.Equal(deletedPagesCount + 1, deletedPages.Count());
        }

        [Fact]
        public void ModelFiresMoveEvent()
        {
            int movedDirectoryCount = movedDirectories.Count();
            int movedPageCount = movedPages.Count();

            string oldFilePath = Path.Combine(".", "index.md");
            string newFilePath = Path.Combine(".", "indexRenamed.md");

            watcher.FireFileMoved(newFilePath, oldFilePath);

            // make sure the event gets a chance to be noticed
            System.Threading.Thread.Sleep(10);

            // no more directories moved
            Assert.Equal(movedDirectoryCount, movedDirectories.Count());

            // 1 more page moved
            Assert.Equal(movedPageCount + 1, movedPages.Count());
        }

        [Fact]
        public void ModelFiresOnlyAddOnChangeOfExtensionToMd()
        {
            int createdDirectoryCount = createdDirectories.Count();
            int createdPagesCount = createdPages.Count();

            int deletedDirectoryCount = deletedDirectories.Count();
            int deletedPagesCount = deletedPages.Count();

            // note: This wouldn't be called because the file watcher would never add this file!
            // removing this line means the following is a valid test
            //model.AddPage(Path.Combine(".", "subdir1", "index.txt"));

            // no more directories created
            Assert.Equal(createdDirectoryCount, createdDirectories.Count());
            Assert.Equal(deletedDirectoryCount, deletedDirectories.Count());

            // no more pages created (because it's a .txt file)
            Assert.Equal(createdPagesCount, createdPages.Count());
            Assert.Equal(deletedPagesCount, deletedPages.Count());

            string oldFilePath = Path.Combine(".", "subdir1", "index.txt");
            string newFilePath = Path.Combine(".", "subdir1", "index.md");

            // now rename the file
            watcher.FireFileMoved(newFilePath, oldFilePath);

            // make sure the event gets a chance to be noticed
            System.Threading.Thread.Sleep(10);

            // no more directories deleted or created
            Assert.Equal(createdDirectoryCount, createdDirectories.Count());
            Assert.Equal(deletedDirectoryCount, deletedDirectories.Count());

            // 1 more pages created, nothing deleted
            Assert.Equal(createdPagesCount + 1, createdPages.Count());
            Assert.Equal(deletedPagesCount, deletedPages.Count());
        }

        [Fact]
        public void ModelFiresOnlyDeleteOnChangeOfExtensionToTxt()
        {
            int createdDirectoryCount = createdDirectories.Count();
            int createdPagesCount = createdPages.Count();

            int deletedDirectoryCount = deletedDirectories.Count();
            int deletedPagesCount = deletedPages.Count();

            watcher.FireFileAdded(Path.Combine(".", "subdir1", "index.md"));

            // make sure the event gets a chance to be noticed
            System.Threading.Thread.Sleep(10);

            // no more directories created
            Assert.Equal(createdDirectoryCount, createdDirectories.Count());
            Assert.Equal(deletedDirectoryCount, deletedDirectories.Count());

            // 1 more pages created
            Assert.Equal(createdPagesCount + 1, createdPages.Count());
            Assert.Equal(deletedPagesCount, deletedPages.Count());

            string oldFilePath = Path.Combine(".", "subdir1", "index.md");
            string newFilePath = Path.Combine(".", "subdir1", "index.txt");

            // now rename the file
            watcher.FireFileMoved(newFilePath, oldFilePath);

            // make sure the event gets a chance to be noticed
            System.Threading.Thread.Sleep(10);

            // no more directories deleted or created
            Assert.Equal(createdDirectoryCount, createdDirectories.Count());
            Assert.Equal(deletedDirectoryCount, deletedDirectories.Count());

            // 1 more pages deleted, no more created
            Assert.Equal(createdPagesCount + 1, createdPages.Count());
            Assert.Equal(deletedPagesCount + 1, deletedPages.Count());
        }

        [Fact]
        public void ModelFiresNothingWhenEmptyDirectoryIsAdded()
        {
            int createdDirectoryCount = createdDirectories.Count();
            int createdPagesCount = createdPages.Count();

            watcher.FireDirectoryAdded(Path.Combine(".", "subdir1", "subdir2", "subdir3"));

            // make sure the event gets a chance to be noticed
            System.Threading.Thread.Sleep(10);

            // no more directories created
            Assert.Equal(createdDirectoryCount, createdDirectories.Count());

            // no more pages created
            Assert.Equal(createdPagesCount, createdPages.Count());
        }

        [Fact]
        public void ModelFiresPageCreatedAndDirectoryCreatedEvent()
        {
            int createdDirectoryCount = createdDirectories.Count();
            int createdPagesCount = createdPages.Count();

            watcher.FireFileAdded(Path.Combine(".", "subdir1", "subdir2", "subdir3", "myfile.md"));

            // make sure the event gets a chance to be noticed
            System.Threading.Thread.Sleep(10);

            // 1 more directories created
            Assert.Equal(createdDirectoryCount + 1, createdDirectories.Count());

            // 1 more pages created
            Assert.Equal(createdPagesCount + 1, createdPages.Count());
        }

        [Fact]
        public void ModelFiresDirectoryUpdatedEvent()
        {
            int updatedDirectoriesCount = updatedDirectories.Count();
            int updatedPagesCount = updatedPages.Count();

            watcher.FireDirectoryUpdated(Path.Combine(".", "subdir1", "subdir2"));

            // make sure the event gets a chance to be noticed
            System.Threading.Thread.Sleep(10);

            // no more directories updated
            Assert.Equal(updatedDirectoriesCount + 1, updatedDirectories.Count());

            // no more pages updated
            Assert.Equal(updatedPagesCount, updatedPages.Count());
        }

        [Fact]
        public void ModelFiresDirectoryDeletedEvent()
        {
            int deletedDirectoriesCount = deletedDirectories.Count();
            int deletedPagesCount = deletedPages.Count();

            // note: because a file watcher may or may not trigger a "deletepage" event
            // the delete directory event should recursively delete all contents of a deleted directory
            watcher.FireDirectoryDeleted(Path.Combine(".", "subdir1", "subdir2"));

            // make sure the event gets a chance to be noticed
            System.Threading.Thread.Sleep(10);

            // 2 more directories updated (subdir1 and subdir2)
            Assert.Equal(deletedDirectoriesCount + 2, deletedDirectories.Count());

            // 1 more pages updated
            Assert.Equal(deletedPagesCount + 1, deletedPages.Count());
        }

        [Fact]
        public void ModelFiresMoveDirectoryEvent()
        {
            int movedDirectoryCount = movedDirectories.Count();
            int movedPagesCount = movedPages.Count();

            string oldPath = Path.Combine(".", "subdir1", "subdir2");
            string newPath = Path.Combine(".", "subdir1", "subdirSecond");

            watcher.FireDirectoryMoved(newPath, oldPath);

            // make sure the event gets a chance to be noticed
            System.Threading.Thread.Sleep(100);

            // 1 more directories moved
            Assert.Equal(movedDirectoryCount + 1, movedDirectories.Count());

            // 1 more page moved
            Assert.Equal(movedPagesCount + 1, movedPages.Count());
        }
    }
}
