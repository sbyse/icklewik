using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Icklewik.Core.Model
{
    public class WikiModelEventArgs : WikiEventArgs
    {
        private static int DebugCounter = 100;

        private int debugCount;

        public WikiModelEventArgs(
            string sourcePath,
            string wikiPath,
            string wikiUrl,
            string oldSourcePath,
            string oldWikiPath,
            string oldWikiUrl)
        {
            SourcePath = sourcePath;
            WikiPath = wikiPath;
            WikiUrl = wikiUrl;
            OldSourcePath = oldSourcePath;
            OldWikiPath = oldWikiPath;
            OldWikiUrl = oldWikiUrl;

            debugCount = DebugCounter++;
        }

        // relative path to source file
        public string SourcePath { get; private set; }

        // relative path to wiki page (where it's stored on the file system)
        public string WikiPath { get; private set; }

        // relative url to wiki page
        public string WikiUrl { get; private set; }

        // used for "move" events only
        public string OldSourcePath { get; private set; }

        // used for "move" events only
        public string OldWikiPath { get; private set; }

        // used for "move" events only
        public string OldWikiUrl { get; private set; }

        public override string Id
        {
            get
            {
                return string.Format("#{0} {1}", debugCount, SourcePath);
            }
        }
    }
}
