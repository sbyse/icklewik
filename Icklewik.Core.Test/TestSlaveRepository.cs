using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using Icklewik.Core.Model;
using Xunit;

namespace Icklewik.Core.Test
{
    public class TestEventSource : IWikiModelEventSource
    {
        public event Action<object, EventSourceInitialisedArgs> EventSourceStarted;

        public event Action<object, WikiModelEventArgs> PageAdded;
        public event Action<object, WikiModelEventArgs> PageUpdated;
        public event Action<object, WikiModelEventArgs> PageDeleted;
        public event Action<object, WikiModelEventArgs> PageMoved;
        public event Action<object, WikiModelEventArgs> DirectoryAdded;
        public event Action<object, WikiModelEventArgs> DirectoryUpdated;
        public event Action<object, WikiModelEventArgs> DirectoryDeleted;
        public event Action<object, WikiModelEventArgs> DirectoryMoved;

        public string RootSourcePath { get; set; }
        public string RootWikiPath { get; set; }

        public void FireEventSourceStarted()
        {
            if (EventSourceStarted != null)
            {
                EventSourceStarted(this, new EventSourceInitialisedArgs());
            }
        }

        public void FirePageAdded(WikiPage page)
        {
            FireEvent(PageAdded, page);
        }

        public void FirePageUpdated(WikiPage page)
        {
            FireEvent(PageUpdated, page);
        }

        public void FirePageDeleted(WikiPage page)
        {
            FireEvent(PageDeleted, page);
        }

        public void FirePageMoved(WikiPage page, string oldSourcePath, string oldWikiPath)
        {
            FireEvent(PageMoved, page, oldSourcePath, oldWikiPath);
        }

        public void FireDirectoryAdded(WikiDirectory page)
        {
            FireEvent(DirectoryAdded, page);
        }

        public void FireDirectoryUpdated(WikiDirectory page)
        {
            FireEvent(DirectoryUpdated, page);
        }

        public void FireDirectoryDeleted(WikiDirectory page)
        {
            FireEvent(DirectoryDeleted, page);
        }

        public void FireDirectoryMoved(WikiDirectory page, string oldSourcePath, string oldWikiPath)
        {
            FireEvent(DirectoryMoved, page, oldSourcePath, oldWikiPath);
        }

        private void FireEvent(Action<object, WikiModelEventArgs> eventToFire, WikiEntry entry, string oldSourcePath = "", string oldWikiPath = "")
        {
            if (eventToFire != null)
            {
                eventToFire(this, new WikiModelEventArgs(
                    sourcePath: entry.SourcePath,
                    wikiPath: entry.WikiPath,
                    wikiUrl: entry.WikiUrl,
                    oldSourcePath: oldSourcePath,
                    oldWikiPath: oldWikiPath));
            }
        }
    }

    /// <summary>
    /// Tests the slave repository
    /// </summary>
    public class TestSlaveRepository
    {
        private SlaveRepository repository;
        private TestEventSource eventSource;

        public TestSlaveRepository()
        {
            repository = new SlaveRepository(".md");

            eventSource = new TestEventSource();
            eventSource.RootSourcePath = PathHelper.GetFullPath(".");
            eventSource.RootWikiPath = PathHelper.GetFullPath(".", "somewikipath");

            // initialise the model
            repository.Init(eventSource, Scheduler.Immediate);

            eventSource.FireEventSourceStarted();
        }

        [Fact]
        public void ModelInitialisationCorrect()
        {
            // in the case of the slave repo initialisation wires up the event source and
            // the root directory, nothing else

            // check that the total asset count is correct
            Assert.Equal(1, repository.Model.AvailableAssets.Count());
            Assert.Equal(1, repository.Model.AvailableAssets.Count(path => repository.Model.GetAsset(path) is WikiDirectory));
            
            Assert.Equal(eventSource.RootSourcePath, repository.RootSourcePath);
            Assert.Equal(eventSource.RootWikiPath, repository.RootWikiPath);

            Assert.Equal(PathHelper.GetFullPath("."), repository.Model.GetAsset(PathHelper.GetFullPath(".")).SourcePath);
        }

        [Fact]
        public void HandlesDirectoryAdded()
        {
            Assert.Equal(1, repository.Model.AvailableAssets.Count());
            Assert.Equal(1, repository.Model.AvailableAssets.Count(path => repository.Model.GetAsset(path) is WikiDirectory));

            eventSource.FireDirectoryAdded(
                new WikiDirectory
                {
                    SourcePath = ".",
                    LastUpdated = DateTime.UtcNow,
                    Depth = 1,
                    Parent = null,
                    Children = new List<WikiEntry>()
                });

            // NOTE: Adding an empty directory does nothing (the root directory is already there)
            Assert.Equal(1, repository.Model.AvailableAssets.Count()); 
        }

        [Fact]
        public void HandlesPageAdded()
        {
            Assert.Equal(1, repository.Model.AvailableAssets.Count());
            Assert.Equal(1, repository.Model.AvailableAssets.Count(path => repository.Model.GetAsset(path) is WikiDirectory));

            int rootDepth = repository.Model.GetAsset(PathHelper.GetFullPath(".")).Depth;

            eventSource.FirePageAdded(
                new WikiPage
                {
                    SourcePath = "myFile.md",
                    LastUpdated = DateTime.UtcNow,
                    Depth = 10,
                    Parent = null
                });

            Assert.Equal(2, repository.Model.AvailableAssets.Count());
            Assert.Equal(1, repository.Model.AvailableAssets.Count(path => repository.Model.GetAsset(path) is WikiPage));

            WikiEntry asset = repository.Model.GetAsset(PathHelper.GetFullPath("myFile.md"));
            Assert.Equal(PathHelper.GetFullPath("myFile.md"), asset.SourcePath);
            Assert.Equal(repository.Model.GetAsset(PathHelper.GetFullPath(".")), asset.Parent);
            Assert.Equal(rootDepth + 1, asset.Depth);
        }

        //[Fact]
        //public void ModelFiresPageCreatedEvent()
        //{
        //    int createdDirectoryCount = createdDirectories.Count();
        //    int createdPagesCount = createdPages.Count();

        //    repository.AddPage(Path.Combine(".", "subdir1", "index.md"));
        //    repository.AddPage(Path.Combine(".", "firstFile.md"));

        //    // no more directories created
        //    Assert.Equal(createdDirectoryCount, createdDirectories.Count());

        //    // 2 more page created
        //    Assert.Equal(createdPagesCount + 2, createdPages.Count());
        //}

        //[Fact]
        //public void ModelFiresPageUpdatedEvent()
        //{
        //    int updatedDirectoryCount = updatedDirectories.Count();
        //    int updatedPagesCount = updatedPages.Count();

        //    repository.UpdatePage(Path.Combine(".", "subdir1", "subdir2", "index.md"));
        //    repository.UpdatePage(Path.Combine(".", "index.md"));

        //    // no more directories updated
        //    Assert.Equal(updatedDirectoryCount, updatedDirectories.Count());

        //    // 2 more pages updated
        //    Assert.Equal(updatedPagesCount + 2, updatedPages.Count());
        //}

        //[Fact]
        //public void ModelFiresPageDeletedEvent()
        //{
        //    int deletedDirectoryCount = deletedDirectories.Count();
        //    int deletedPagesCount = deletedPages.Count();

        //    repository.DeletePage(Path.Combine(".", "index.md"));

        //    // no more directories deleted
        //    Assert.Equal(deletedDirectoryCount, deletedDirectories.Count());

        //    // 1 more pages deleted
        //    Assert.Equal(deletedPagesCount + 1, deletedPages.Count());
        //}

        //[Fact]
        //public void ModelFiresPageDeletedAndDirectoryDeletedEvent()
        //{
        //    int deletedDirectoryCount = deletedDirectories.Count();
        //    int deletedPagesCount = deletedPages.Count();

        //    repository.DeletePage(Path.Combine(".", "subdir1", "subdir2", "index.md"));

        //    // 2 more directories deleted (subdir1 and subdir2)
        //    Assert.Equal(deletedDirectoryCount + 2, deletedDirectories.Count());

        //    // 1 more pages deleted
        //    Assert.Equal(deletedPagesCount + 1, deletedPages.Count());
        //}

        //[Fact]
        //public void ModelFiresMoveEvent()
        //{
        //    int movedDirectoryCount = movedDirectories.Count();
        //    int movedPageCount = movedPages.Count();

        //    repository.RenamePage(Path.Combine(".", "index.md"), Path.Combine(".", "indexRenamed.md"));

        //    // no more directories moved
        //    Assert.Equal(movedDirectoryCount, movedDirectories.Count());

        //    // 1 more page moved
        //    Assert.Equal(movedPageCount + 1, movedPages.Count());
        //}

        //[Fact]
        //public void ModelFiresOnlyAddOnChangeOfExtensionToMd()
        //{
        //    int createdDirectoryCount = createdDirectories.Count();
        //    int createdPagesCount = createdPages.Count();

        //    int deletedDirectoryCount = deletedDirectories.Count();
        //    int deletedPagesCount = deletedPages.Count();

        //    // note: This wouldn't be called because the file watcher would never add this file!
        //    // removing this line means the following is a valid test
        //    //model.AddPage(Path.Combine(".", "subdir1", "index.txt"));

        //    // no more directories created
        //    Assert.Equal(createdDirectoryCount, createdDirectories.Count());
        //    Assert.Equal(deletedDirectoryCount, deletedDirectories.Count());

        //    // no more pages created (because it's a .txt file)
        //    Assert.Equal(createdPagesCount, createdPages.Count());
        //    Assert.Equal(deletedPagesCount, deletedPages.Count());

        //    // now rename the file
        //    repository.RenamePage(Path.Combine(".", "subdir1", "index.txt"), Path.Combine(".", "subdir1", "index.md"));

        //    // no more directories deleted or created
        //    Assert.Equal(createdDirectoryCount, createdDirectories.Count());
        //    Assert.Equal(deletedDirectoryCount, deletedDirectories.Count());

        //    // 1 more pages created, nothing deleted
        //    Assert.Equal(createdPagesCount + 1, createdPages.Count());
        //    Assert.Equal(deletedPagesCount, deletedPages.Count());
        //}

        //[Fact]
        //public void ModelFiresOnlyDeleteOnChangeOfExtensionToTxt()
        //{
        //    int createdDirectoryCount = createdDirectories.Count();
        //    int createdPagesCount = createdPages.Count();

        //    int deletedDirectoryCount = deletedDirectories.Count();
        //    int deletedPagesCount = deletedPages.Count();

        //    repository.AddPage(Path.Combine(".", "subdir1", "index.md"));

        //    // no more directories created
        //    Assert.Equal(createdDirectoryCount, createdDirectories.Count());
        //    Assert.Equal(deletedDirectoryCount, deletedDirectories.Count());

        //    // 1 more pages created
        //    Assert.Equal(createdPagesCount + 1, createdPages.Count());
        //    Assert.Equal(deletedPagesCount, deletedPages.Count());

        //    // now rename the file
        //    repository.RenamePage(Path.Combine(".", "subdir1", "index.md"), Path.Combine(".", "subdir1", "index.txt"));

        //    // no more directories deleted or created
        //    Assert.Equal(createdDirectoryCount, createdDirectories.Count());
        //    Assert.Equal(deletedDirectoryCount, deletedDirectories.Count());

        //    // 1 more pages deleted, no more created
        //    Assert.Equal(createdPagesCount + 1, createdPages.Count());
        //    Assert.Equal(deletedPagesCount + 1, deletedPages.Count());
        //}

        //[Fact]
        //public void ModelFiresNothingWhenEmptyDirectoryIsAdded()
        //{
        //    int createdDirectoryCount = createdDirectories.Count();
        //    int createdPagesCount = createdPages.Count();

        //    repository.AddDirectory(Path.Combine(".", "subdir1", "subdir2", "subdir3"));

        //    // no more directories created
        //    Assert.Equal(createdDirectoryCount, createdDirectories.Count());

        //    // no more pages created
        //    Assert.Equal(createdPagesCount, createdPages.Count());
        //}

        //[Fact]
        //public void ModelFiresPageCreatedAndDirectoryCreatedEvent()
        //{
        //    int createdDirectoryCount = createdDirectories.Count();
        //    int createdPagesCount = createdPages.Count();

        //    repository.AddPage(Path.Combine(".", "subdir1", "subdir2", "subdir3", "myfile.md"));

        //    // 1 more directories created
        //    Assert.Equal(createdDirectoryCount + 1, createdDirectories.Count());

        //    // 1 more pages created
        //    Assert.Equal(createdPagesCount + 1, createdPages.Count());
        //}

        //[Fact]
        //public void ModelFiresDirectoryUpdatedEvent()
        //{
        //    int updatedDirectoriesCount = updatedDirectories.Count();
        //    int updatedPagesCount = updatedPages.Count();

        //    repository.UpdateDirectory(Path.Combine(".", "subdir1", "subdir2"));

        //    // no more directories updated
        //    Assert.Equal(updatedDirectoriesCount + 1, updatedDirectories.Count());

        //    // no more pages updated
        //    Assert.Equal(updatedPagesCount, updatedPages.Count());
        //}

        //[Fact]
        //public void ModelFiresDirectoryDeletedEvent()
        //{
        //    int deletedDirectoriesCount = deletedDirectories.Count();
        //    int deletedPagesCount = deletedPages.Count();

        //    // note: because a file watcher may or may not trigger a "deletepage" event
        //    // the delete directory event should recursively delete all contents of a deleted directory
        //    repository.DeleteDirectory(Path.Combine(".", "subdir1", "subdir2"));

        //    // 2 more directories updated (subdir1 and subdir2)
        //    Assert.Equal(deletedDirectoriesCount + 2, deletedDirectories.Count());

        //    // 1 more pages updated
        //    Assert.Equal(deletedPagesCount + 1, deletedPages.Count());
        //}

        //[Fact]
        //public void ModelFiresMoveDirectoryEvent()
        //{
        //    int movedDirectoryCount = movedDirectories.Count();
        //    int movedPagesCount = movedPages.Count();

        //    repository.RenameDirectory(Path.Combine(".", "subdir1", "subdir2"), Path.Combine(".", "subdir1", "subdirSecond"));

        //    // 1 more directories moved
        //    Assert.Equal(movedDirectoryCount + 1, movedDirectories.Count());

        //    // 1 more page moved
        //    Assert.Equal(movedPagesCount + 1, movedPages.Count());
        //}
    }
}
