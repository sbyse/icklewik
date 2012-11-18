using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Icklewik.Core
{
    public class WikiGenerator
    {
        private string sourceRoot;
        private string wikiRoot;
        private Convertor convertor;

        public WikiGenerator(string rootSourcePath, string rootWikiPath, Convertor sourceConvertor)
        {
            sourceRoot = rootSourcePath;
            wikiRoot = rootWikiPath;
            convertor = sourceConvertor;
        }

        public void CreateDirectory(WikiDirectory directory)
        {
            Directory.CreateDirectory(directory.WikiPath);

            directory.LastGenerated = DateTime.UtcNow;
        }

        public void UpdateDirectory(WikiDirectory directory)
        {
            directory.LastGenerated = DateTime.UtcNow;
        }

        public void DeleteDirectory(WikiDirectory directory)
        {
            Directory.Delete(directory.WikiPath);
        }

        public void CreatePage(WikiPage page)
        {
            UpdatePage(page);
        }

        public void UpdatePage(WikiPage page)
        {
            // actually parse the markdown
            string convertedHtml = convertor.Convert(File.ReadAllText(page.MarkdownPath));

            // and write to the wiki location
            File.WriteAllText(page.WikiPath, convertedHtml, Encoding.UTF8);

            page.LastGenerated = DateTime.UtcNow;
        }

        public void DeletePage(WikiPage page)
        {
            File.Delete(page.WikiPath);
        }
    }
}
