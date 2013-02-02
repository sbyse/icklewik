using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Icklewik.Core.Model
{
    /// <summary>
    /// Represents the underlying data model for a wiki site, it contains details of all directories and pages
    /// 
    /// Uses a lock policy to allow for different locking behaviour
    /// </summary>
    public class WikiModel
    {
        // root directory represents the base node of the source tree, the tree can
        // be traversed from this point if required
        private WikiDirectory rootDirectory;

        private WikiPathComparer pathComparer;

        // we also keep all entries in a map for simple retrieval of specific entries
        private IDictionary<string, WikiEntry> wikiEntryMap;

        public WikiModel()
        {
            pathComparer = new WikiPathComparer();

            wikiEntryMap = new Dictionary<string, WikiEntry>(pathComparer);
        }

        /// <summary>
        /// Represents the file system location at the top of the source tree
        /// </summary>
        public string RootSourcePath
        {
            get
            {
                return rootDirectory.SourcePath;
            }
        }

        /// <summary>
        /// Represents the file system location that holds the generated wiki
        /// files. The files in this location should be treated as temporary and will
        /// be regenerated in response to changes in the root source path
        /// </summary>
        public string RootWikiPath
        {
            get
            {
                return rootDirectory.WikiPath;
            }
        }

        public WikiPathComparer PathComparer
        {
            get
            {
                return pathComparer;
            }
        }

        public IEnumerable<string> AvailableAssets
        {
            get
            {
                return wikiEntryMap.Keys;
            }
        }

        public bool ContainsAsset(string fullPath)
        {
            return wikiEntryMap.ContainsKey(fullPath);
        }

        public WikiEntry GetAsset(string fullPath)
        {
            return wikiEntryMap[fullPath];
        }

        public bool TryGetAsset(string fullPath, out WikiEntry entry)
        {
            return wikiEntryMap.TryGetValue(fullPath, out entry);
        }

        public void AddAsset(string fullPath, WikiEntry entry)
        {
            wikiEntryMap[fullPath] = entry;
        }

        public void RemoveAsset(string fullPath)
        {
            wikiEntryMap.Remove(fullPath);
        }

        public void SetRootDirectory(WikiDirectory root)
        {
            rootDirectory = root;
        }
    }
}
