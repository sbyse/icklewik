using Xunit;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Icklewik.Core.Test
{
    /// <summary>
    /// Tests the model without actually needing to add files to the file system
    /// </summary>
    public class TestWikiModel
    {
        private IList<WikiDirectory> createdDirectories;
        private IList<WikiDirectory> updatedDirectories;
        private IList<WikiDirectory> deletedDirectories;
        private IList<WikiDirectory> movedDirectories;

        private IList<WikiPage> createdPages;
        private IList<WikiPage> updatedPages;
        private IList<WikiPage> deletedPages;
        private IList<WikiPage> movedPages;

        private WikiModel model;

        public TestWikiModel()
        {
            createdDirectories = new List<WikiDirectory>();
            updatedDirectories = new List<WikiDirectory>();
            deletedDirectories = new List<WikiDirectory>();
            movedDirectories = new List<WikiDirectory>();

            createdPages = new List<WikiPage>();
            updatedPages = new List<WikiPage>();
            deletedPages = new List<WikiPage>();
            movedPages = new List<WikiPage>();

            model = new WikiModel(".md");

            // setup event handlers
            model.DirectoryAdded += (directory) => createdDirectories.Add(directory);
            model.DirectoryUpdated += (directory) => updatedDirectories.Add(directory);
            model.DirectoryDeleted += (directory) => deletedDirectories.Add(directory);
            model.DirectoryMoved += (oldWikiPath, directory) => movedDirectories.Add(directory);

            model.PageAdded += (page) => createdPages.Add(page);
            model.PageUpdated += (page) => updatedPages.Add(page);
            model.PageDeleted += (page) => deletedPages.Add(page);
            model.PageMoved += (oldWikiPath, page) => movedPages.Add(page);

            // create list of files
            IList<string> markdownFiles = new List<string>
                {
                    Path.Combine(".", "index.md"),
                    Path.Combine(".", "subdir1", "subdir2", "index.md")
                };

            // initialise the model (should fire events the same as addition)
            model.Init(".", Path.Combine(".", "somewikipath"), markdownFiles);
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

            model.AddPage(Path.Combine(".", "subdir1", "index.md"));
            model.AddPage(Path.Combine(".", "firstFile.md"));

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

            model.UpdatePage(Path.Combine(".", "subdir1", "subdir2", "index.md"));
            model.UpdatePage(Path.Combine(".", "index.md"));

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

            model.DeletePage(Path.Combine(".", "index.md"));

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

            model.DeletePage(Path.Combine(".", "subdir1", "subdir2", "index.md"));

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

            model.RenamePage(Path.Combine(".", "index.md"), Path.Combine(".", "indexRenamed.md"));

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

            // now rename the file
            model.RenamePage(Path.Combine(".", "subdir1", "index.txt"), Path.Combine(".", "subdir1", "index.md"));

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

            model.AddPage(Path.Combine(".", "subdir1", "index.md"));

            // no more directories created
            Assert.Equal(createdDirectoryCount, createdDirectories.Count());
            Assert.Equal(deletedDirectoryCount, deletedDirectories.Count());

            // 1 more pages created
            Assert.Equal(createdPagesCount + 1, createdPages.Count());
            Assert.Equal(deletedPagesCount, deletedPages.Count());

            // now rename the file
            model.RenamePage(Path.Combine(".", "subdir1", "index.md"), Path.Combine(".", "subdir1", "index.txt"));

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

            model.AddDirectory(Path.Combine(".", "subdir1", "subdir2", "subdir3"));

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

            model.AddPage(Path.Combine(".", "subdir1", "subdir2", "subdir3", "myfile.md"));

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

            model.UpdateDirectory(Path.Combine(".", "subdir1", "subdir2"));

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
            model.DeleteDirectory(Path.Combine(".", "subdir1", "subdir2"));

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

            model.RenameDirectory(Path.Combine(".", "subdir1", "subdir2"), Path.Combine(".", "subdir1", "subdirSecond"));

            // 1 more directories moved
            Assert.Equal(movedDirectoryCount + 1, movedDirectories.Count());

            // 1 more page moved
            Assert.Equal(movedPagesCount + 1, movedPages.Count());
        }
    }
}
