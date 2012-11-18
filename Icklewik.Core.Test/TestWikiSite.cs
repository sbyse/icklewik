using Xunit;
using System.IO;
using System;

namespace Icklewik.Core.Test
{
    public class TestWikiSite : IDisposable
    {
        private WikiSite site;

        public TestWikiSite()
        {
            // setup initial site structure
            Directory.CreateDirectory(Path.Combine(".", "subdir1"));
            Directory.CreateDirectory(Path.Combine(".", "subdir1", "subdir2"));
            Directory.CreateDirectory(Path.Combine(".", "subdir1", "subdir2", "subdir3"));
            Directory.CreateDirectory(Path.Combine(".", "subdir1", "subdir2", "subdir3", "subdir4"));
            File.AppendAllText(Path.Combine(".", "index.md"), "Hello World");
            File.AppendAllText(Path.Combine(".", "subdir1", "index.md"), "Hello World Sub Directory");
            File.AppendAllText(Path.Combine(".", "subdir1", "subdir2", "index.md"), "Hello World Sub Sub Directory");

            site = new WikiSite(".", Path.Combine(".", "wiki"), new Convertor(new MarkdownSharpDialogue()));

            site.Start();
        }

        public void Dispose()
        {
            site.Dispose();

            Directory.Delete(Path.Combine(".", "wiki"), true);
            Directory.Delete(Path.Combine(".", "subdir1"), true);

            foreach (string filePath in Directory.EnumerateFiles(".", "*.md", SearchOption.TopDirectoryOnly))
            {
                File.Delete(filePath);
            }
        }

        [Fact]
        public void InitialisationWorks()
        {
            // check that we've created the correct directories and files
            Assert.True(Directory.Exists(Path.Combine(".", "wiki")));
            Assert.True(Directory.Exists(Path.Combine(".", "wiki", "subdir1")));
            Assert.True(Directory.Exists(Path.Combine(".", "wiki", "subdir1", "subdir2")));

            Assert.True(File.Exists(Path.Combine(".", "wiki", "index.html")));
            Assert.True(File.Exists(Path.Combine(".", "wiki", "subdir1", "index.html")));
            Assert.True(File.Exists(Path.Combine(".", "wiki", "subdir1", "subdir2", "index.html")));

            // empty directories have not been added
            Assert.False(Directory.Exists(Path.Combine(".", "wiki", "subdir1", "subdir2", "subdir3")));
            Assert.False(Directory.Exists(Path.Combine(".", "wiki", "subdir1", "subdir2", "subdir3", "subdir4")));
        }

        [Fact]
        public void AddingASourceFileCreatesAWikiPage()
        {
            File.AppendAllText(Path.Combine(".", "firstFile.md"), "Hello Again");
            File.AppendAllText(Path.Combine(".", "subdir1", "subdir2", "subdir3", "subdir4", "index.md"), "Index in subdir 4");

            System.Threading.Thread.Sleep(1000);

            Assert.True(File.Exists(Path.Combine(".", "wiki", "firstFile.html")));
            Assert.True(File.Exists(Path.Combine(".", "wiki", "subdir1", "subdir2", "subdir3", "subdir4", "index.html")));
        }

        [Fact]
        public void DeletingASourceFileDeletesAWikiPage()
        {
            File.Delete(Path.Combine(".", "index.md"));

            System.Threading.Thread.Sleep(1000);

            Assert.False(File.Exists(Path.Combine(".", "wiki", "index.html")));
        }

        [Fact]
        public void DeletingADirectoryDeletesAllChildDirectoriesAndPages()
        {
            // first add something at the bottom of the tree
            File.AppendAllText(Path.Combine(".", "subdir1", "subdir2", "subdir3", "subdir4", "index.md"), "Index in subdir 4");

            System.Threading.Thread.Sleep(2000);

            // and make sure its been added
            Assert.True(File.Exists(Path.Combine(".", "wiki", "subdir1", "subdir2", "subdir3", "subdir4", "index.html")));

            // then delete a directory in the middle
            Directory.Delete(Path.Combine(".", "subdir1", "subdir2"), true);

            System.Threading.Thread.Sleep(5000);

            Assert.False(File.Exists(Path.Combine(".", "wiki", "subdir1", "subdir2", "index.html")));
            Assert.False(Directory.Exists(Path.Combine(".", "wiki", "subdir1", "subdir2")));
        }

        [Fact]
        public void RenamingToARelevantFileExtensionCreatesPage()
        {
            // note: This wouldn't be called because the file watcher would never add this file!
            // removing this line means the following is a valid test
            File.AppendAllText(Path.Combine(".", "subdir1", "hello.txt"), "Hello Again");

            System.Threading.Thread.Sleep(1000);

            // no file created
            Assert.False(File.Exists(Path.Combine(".", "wiki", "subdir1", "hello.txt")));
            Assert.False(File.Exists(Path.Combine(".", "wiki", "subdir1", "hello.html")));

            // now rename the file
            File.Move(Path.Combine(".", "subdir1", "hello.txt"), Path.Combine(".", "subdir1", "hello.md"));

            System.Threading.Thread.Sleep(2000);

            Assert.True(File.Exists(Path.Combine(".", "wiki", "subdir1", "hello.html")));
        }

        [Fact]
        public void RenamingToAnIrrelevantFileExtensionDeletesPage()
        {
            File.AppendAllText(Path.Combine(".", "subdir1", "hello.md"), "Hello Again");

            System.Threading.Thread.Sleep(1000);

            // no file created
            Assert.True(File.Exists(Path.Combine(".", "wiki", "subdir1", "hello.html")));

            // now rename the file
            File.Move(Path.Combine(".", "subdir1", "hello.md"), Path.Combine(".", "subdir1", "hello.txt"));

            System.Threading.Thread.Sleep(2000);

            Assert.False(File.Exists(Path.Combine(".", "wiki", "subdir1", "hello.html")));
        }
    }
}
