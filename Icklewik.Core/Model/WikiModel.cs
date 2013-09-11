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

        // we also keep all entries in a map for simple retrieval of specific entries.
        // key is full path of source file
        private IDictionary<string, WikiEntry> wikiEntryMap;

        // cached immutable map, this is destroyed everytime any changes are made and
        // recreated on request
        // key is full path of source file
        private IDictionary<string, ImmutableWikiEntry> lazyImmutableWikiEntrySourcePathMap;
        
        // same by key is wiki url
        private IDictionary<string, ImmutableWikiEntry> lazyImmutableWikiEntryWikiUrlMap;

        private object locker = new object();

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

        public IEnumerable<string> AvailableImmutableAssetSourcePaths
        {
            get
            {
                lock (locker)
                {
                    CreateImmutableMaps();
                    return lazyImmutableWikiEntrySourcePathMap.Keys;
                }
            }
        }

        public IEnumerable<ImmutableWikiEntry> GetImmutableWikiEntries()
        {
            lock (locker)
            {
                CreateImmutableMaps();
                return lazyImmutableWikiEntrySourcePathMap.Values;
            }
        }

        public ImmutableWikiEntry GetImmutableAssetBySourcePath(string fullPath)
        {
            lock (locker)
            {
                CreateImmutableMaps();
                return lazyImmutableWikiEntrySourcePathMap[fullPath];
            }
        }

        public bool ContainsAssetBySourcePath(string fullPath)
        {
            lock (locker)
            {
                CreateImmutableMaps();
                return lazyImmutableWikiEntrySourcePathMap.ContainsKey(fullPath);
            }
        }

        public ImmutableWikiEntry GetImmutableAssetByWikiUrl(string url)
        {
            lock (locker)
            {
                CreateImmutableMaps();
                return lazyImmutableWikiEntryWikiUrlMap[url];
            }
        }

        public bool ContainsAssetByWikiUrl(string url)
        {
            lock (locker)
            {
                CreateImmutableMaps();
                return lazyImmutableWikiEntryWikiUrlMap.ContainsKey(url);
            }
        }

        // TODO: Are these two methods required?
        public WikiEntry UnsafeGetAsset(string fullPath)
        {
            return wikiEntryMap[fullPath];
        }

        public bool UnsafeTryGetAssetBySourcePath(string fullPath, out WikiEntry entry)
        {
            return wikiEntryMap.TryGetValue(fullPath, out entry);
        }

        public void AddAsset(string fullPath, WikiEntry entry)
        {
            // add mutable asset
            wikiEntryMap[fullPath] = entry;
            DestroyImmutableMaps();
        }

        public void RemoveAsset(string fullPath)
        {
            wikiEntryMap.Remove(fullPath);
            DestroyImmutableMaps();
        }

        public void SetRootDirectory(WikiDirectory root)
        {
            rootDirectory = root;
        }

        private void DestroyImmutableMaps()
        {
            lock (locker)
            {
                lazyImmutableWikiEntrySourcePathMap = null;
                lazyImmutableWikiEntryWikiUrlMap = null;
            }
        }

        private void CreateImmutableMaps()
        {
            if (lazyImmutableWikiEntrySourcePathMap == null && lazyImmutableWikiEntryWikiUrlMap == null)
            {
                // create immutable map from source map
                ImmutableMapCreationVisitor visitor = new ImmutableMapCreationVisitor();

                rootDirectory.Accept(visitor);

                lazyImmutableWikiEntrySourcePathMap = visitor.CreateFinalSourcePathMap();
                lazyImmutableWikiEntryWikiUrlMap = visitor.CreateFinalWikiUrlMap();
            }
        }
    }

    public class ImmutableMapCreationVisitor : IWikiEntryVisitor
    {
        Stack<WikiDirectory> directoryStack;

        public ImmutableMapCreationVisitor()
        {
            directoryStack = new Stack<WikiDirectory>();
        }

        public void Visit(WikiDirectory directory)
        {
            // add to the stack
            directoryStack.Push(directory);

            foreach (var child in directory.Children)
            {
                child.Accept(this);
            }
        }

        public void Visit(WikiPage page)
        {
            // do nothing
        }

        public IDictionary<string, ImmutableWikiEntry> CreateFinalSourcePathMap()
        {
            return CreateFinalMapBy(we => we.SourcePath);
        }

        public IDictionary<string, ImmutableWikiEntry> CreateFinalWikiUrlMap()
        {
            return CreateFinalMapBy(we => we.WikiUrl);
        }


        private IDictionary<string, ImmutableWikiEntry> CreateFinalMapBy(Func<IWikiEntry, string> keySelector)
        {
            IDictionary<string, ImmutableWikiEntry> map = new Dictionary<string, ImmutableWikiEntry>();
            while (directoryStack.Any())
            {
                WikiDirectory directory = directoryStack.Pop();

                IList<ImmutableWikiEntry> children = new List<ImmutableWikiEntry>();

                foreach (WikiEntry mutableChild in directory.Children)
                {
                    if (mutableChild is WikiPage)
                    {
                        // create the child and add to the directory and to the map
                        ImmutableWikiPage immutablePage = new ImmutableWikiPage(
                            mutableChild.SourcePath,
                            mutableChild.WikiPath,
                            mutableChild.WikiUrl,
                            mutableChild.LastUpdated,
                            mutableChild.Depth);

                        map[keySelector(mutableChild)] = immutablePage;
                        children.Add(immutablePage);
                    }
                    else if (mutableChild is WikiDirectory)
                    {
                        // because we are traversing the branches of the tree from leaf to root
                        // we can assume that any directory children have already been added to the
                        // map
                        children.Add(map[keySelector(mutableChild)]);
                    }
                }

                // now add the directory itself
                ImmutableWikiDirectory immutableDirectory = new ImmutableWikiDirectory(
                    directory.SourcePath,
                    directory.WikiPath,
                    directory.WikiUrl,
                    directory.LastUpdated,
                    directory.Depth,
                    children);

                map[keySelector(immutableDirectory)] = immutableDirectory;
            }

            return map;
        }

    }
}
