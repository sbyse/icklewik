using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Icklewik.Core.Model;
using Icklewik.Core.Site;
using Xunit;
using Icklekwik.Core.Cache;
using Icklewik.Core.Source;

namespace Icklewik.Core.Test
{
    public class TestWikiSite : IDisposable
    {
        private IList<string> createdDirectories;
        private IList<string> updatedDirectories;
        private IList<string> deletedDirectories;
        private IList<string> movedDirectories;

        private IList<string> createdPages;
        private IList<string> updatedPages;
        private IList<string> deletedPages;
        private IList<string> movedPages;

        private WikiSite site;

        public TestWikiSite()
        {
            // setup initial site structure
            Directory.CreateDirectory(Path.Combine(".", "subdir1"));
            Directory.CreateDirectory(Path.Combine(".", "subdir1", "subdir2"));
            Directory.CreateDirectory(Path.Combine(".", "subdir1", "subdir2", "subdir3"));
            Directory.CreateDirectory(Path.Combine(".", "subdir1", "subdir2", "subdir3", "subdir4"));
            System.IO.File.AppendAllText(Path.Combine(".", "index.md"), "Hello World");
            System.IO.File.AppendAllText(Path.Combine(".", "subdir1", "index.md"), "Hello World Sub Directory");
            System.IO.File.AppendAllText(Path.Combine(".", "subdir1", "subdir2", "index.md"), "Hello World Sub Sub Directory");

            createdDirectories = new List<string>();
            updatedDirectories = new List<string>();
            deletedDirectories = new List<string>();
            movedDirectories = new List<string>();

            createdPages = new List<string>();
            updatedPages = new List<string>();
            deletedPages = new List<string>();
            movedPages = new List<string>();

            Convertor convertor = new Convertor(new MarkdownSharpDialogue());
            WikiConfig wikiConfig = new WikiConfig()
                {
                    SiteName = "Tester",
                    RootSourcePath = PathHelper.GetFullPath("."),
                    RootWikiPath = PathHelper.GetFullPath(".", "wiki"),
                    Convertor = new Convertor(new MarkdownSharpDialogue())
                };

            site = new WikiSite(wikiConfig, 
                new MasterRepository(convertor.FileExtension),
                new SourceWatcher(wikiConfig.RootSourcePath, wikiConfig.Convertor.FileSearchString),
                new PageCache()
            );

            // setup event handlers
            site.DirectoryAdded += (source, args) => createdDirectories.Add(args.WikiUrl);
            site.DirectoryUpdated += (source, args) => updatedDirectories.Add(args.WikiUrl);
            site.DirectoryDeleted += (source, args) => deletedDirectories.Add(args.WikiUrl);
            site.DirectoryMoved += (source, args) => movedDirectories.Add(args.WikiUrl);

            site.PageAdded += (source, args) => createdPages.Add(args.WikiUrl);
            site.PageUpdated += (source, args) => updatedPages.Add(args.WikiUrl);
            site.PageDeleted += (source, args) => deletedPages.Add(args.WikiUrl);
            site.PageMoved += (source, args) => movedPages.Add(args.WikiUrl);

            site.Start();

            Thread.Sleep(1000);
        }

        public void Dispose()
        {
            site.Dispose();

            Directory.Delete(Path.Combine(".", "wiki"), true);
            Directory.Delete(Path.Combine(".", "subdir1"), true);

            foreach (string filePath in Directory.EnumerateFiles(".", "*.md", SearchOption.TopDirectoryOnly))
            {
                System.IO.File.Delete(filePath);
            }
        }

        [Fact]
        public void InitialisationWorks()
        {
            // check that we've created the correct directories and files
            Assert.True(Directory.Exists(Path.Combine(".", "wiki")));
            Assert.True(Directory.Exists(Path.Combine(".", "wiki", "subdir1")));
            Assert.True(Directory.Exists(Path.Combine(".", "wiki", "subdir1", "subdir2")));

            Assert.True(System.IO.File.Exists(Path.Combine(".", "wiki", "index.html")));
            Assert.True(System.IO.File.Exists(Path.Combine(".", "wiki", "subdir1", "index.html")));
            Assert.True(System.IO.File.Exists(Path.Combine(".", "wiki", "subdir1", "subdir2", "index.html")));

            // empty directories have not been added
            Assert.False(Directory.Exists(Path.Combine(".", "wiki", "subdir1", "subdir2", "subdir3")));
            Assert.False(Directory.Exists(Path.Combine(".", "wiki", "subdir1", "subdir2", "subdir3", "subdir4")));

            // check that the right events have been fired post-generation
            Assert.Equal(3, createdDirectories.Count());
            Assert.Equal(0, updatedDirectories.Count());
            Assert.Equal(0, deletedDirectories.Count());
            Assert.Equal(0, movedDirectories.Count());

            Assert.Equal(3, createdPages.Count());
            Assert.Equal(0, updatedPages.Count());
            Assert.Equal(0, movedPages.Count());
        }

        [Fact]
        public void AddingASourceFileCreatesAWikiPage()
        {
            int createdDirectoryCount = createdDirectories.Count();
            int createdPagesCount = createdPages.Count();

            System.IO.File.AppendAllText(Path.Combine(".", "firstFile.md"), "Hello Again");
            System.IO.File.AppendAllText(Path.Combine(".", "subdir1", "subdir2", "subdir3", "subdir4", "index.md"), "Index in subdir 4");

            System.Threading.Thread.Sleep(250);

            Assert.True(System.IO.File.Exists(Path.Combine(".", "wiki", "firstFile.html")));
            Assert.True(System.IO.File.Exists(Path.Combine(".", "wiki", "subdir1", "subdir2", "subdir3", "subdir4", "index.html")));

            // 2 more directories created
            Assert.Equal(createdDirectoryCount + 2, createdDirectories.Count());

            // 2 more page created
            Assert.Equal(createdPagesCount + 2, createdPages.Count());
        }

        [Fact]
        public void DeletingASourceFileDeletesAWikiPage()
        {
            int deletedPagesCount = deletedPages.Count();
            int deletedDirectoriesCount = deletedDirectories.Count();

            System.IO.File.Delete(Path.Combine(".", "index.md"));

            System.Threading.Thread.Sleep(1000);

            Assert.False(System.IO.File.Exists(Path.Combine(".", "wiki", "index.html")));

            // no more directories deleted
            Assert.Equal(deletedDirectoriesCount, deletedDirectories.Count());

            // 1 more page created
            Assert.Equal(deletedPagesCount + 1, deletedPages.Count());
        }

        [Fact]
        public void DeletingADirectoryDeletesAllChildDirectoriesAndPages()
        {
            int deletedPagesCount = deletedPages.Count();
            int deletedDirectoriesCount = deletedDirectories.Count();

            // first add something at the bottom of the tree
            System.IO.File.AppendAllText(Path.Combine(".", "subdir1", "subdir2", "subdir3", "subdir4", "index.md"), "Index in subdir 4");

            System.Threading.Thread.Sleep(500);

            // and make sure its been added
            Assert.True(System.IO.File.Exists(Path.Combine(".", "wiki", "subdir1", "subdir2", "subdir3", "subdir4", "index.html")));

            // then delete a directory in the middle
            Directory.Delete(Path.Combine(".", "subdir1", "subdir2"), true);

            System.Threading.Thread.Sleep(2000);

            Assert.False(System.IO.File.Exists(Path.Combine(".", "wiki", "subdir1", "subdir2", "index.html")));
            Assert.False(Directory.Exists(Path.Combine(".", "wiki", "subdir1", "subdir2")));

            // 3 more directories deleted
            Assert.Equal(deletedDirectoriesCount + 3, deletedDirectories.Count());

            // 2 more page deleted
            Assert.Equal(deletedPagesCount + 2, deletedPages.Count());
        }

        [Fact]
        public void RenamingToARelevantFileExtensionCreatesPage()
        {
            int createdDirectoryCount = createdDirectories.Count();
            int createdPagesCount = createdPages.Count();

            // note: This wouldn't be called because the file watcher would never add this file!
            // removing this line means the following is a valid test
            System.IO.File.AppendAllText(Path.Combine(".", "subdir1", "hello.txt"), "Hello Again");

            System.Threading.Thread.Sleep(250);

            // no file created
            Assert.False(System.IO.File.Exists(Path.Combine(".", "wiki", "subdir1", "hello.txt")));
            Assert.False(System.IO.File.Exists(Path.Combine(".", "wiki", "subdir1", "hello.html")));

            // now rename the file
            System.IO.File.Move(Path.Combine(".", "subdir1", "hello.txt"), Path.Combine(".", "subdir1", "hello.md"));

            System.Threading.Thread.Sleep(250);

            Assert.True(System.IO.File.Exists(Path.Combine(".", "wiki", "subdir1", "hello.html")));

            // no more directories created
            Assert.Equal(createdDirectoryCount, createdDirectories.Count());

            // 1 more page created
            Assert.Equal(createdPagesCount + 1, createdPages.Count());

        }

        [Fact]
        public void RenamingToAnIrrelevantFileExtensionDeletesPage()
        {
            int deletedPagesCount = deletedPages.Count();
            int deletedDirectoriesCount = deletedDirectories.Count();

            System.IO.File.AppendAllText(Path.Combine(".", "subdir1", "hello.md"), "Hello Again");

            System.Threading.Thread.Sleep(250);

            // no file created
            Assert.True(System.IO.File.Exists(Path.Combine(".", "wiki", "subdir1", "hello.html")));

            // now rename the file
            System.IO.File.Move(Path.Combine(".", "subdir1", "hello.md"), Path.Combine(".", "subdir1", "hello.txt"));

            System.Threading.Thread.Sleep(250);

            Assert.False(System.IO.File.Exists(Path.Combine(".", "wiki", "subdir1", "hello.html")));

            // no more directories deleted
            Assert.Equal(deletedDirectoriesCount, deletedDirectories.Count());

            // 1 more page created
            Assert.Equal(deletedPagesCount + 1, deletedPages.Count());
        }
    }
}
