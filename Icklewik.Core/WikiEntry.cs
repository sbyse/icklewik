using System;

namespace Icklewik.Core
{
    public abstract class WikiEntry
    {
        // relative path to source file
        public string MarkdownPath { get; set; }

        // relative path to wiki page (where it's stored on the file system)
        public string WikiPath { get; set; }

        // relative url to wiki page
        public string WikiUrl { get; set; }

        // time that a wiki entry was last updated
        public DateTime LastUpdated { get; set; }

        // time that the wiki page was last updated
        public DateTime? LastGenerated { get; set; }

        // how many levels into the structure are we?
        public int Depth { get; set; }

        // get the parent directory (may be null if this represents the root directory)
        public WikiDirectory Parent { get; set; }

        /// <summary>
        /// visitor pattern, required for functionality that traverses the tree
        /// </summary>
        /// <param name="visitor"></param>
        public abstract void Accept(IWikiEntryVisitor visitor);
    }
}
