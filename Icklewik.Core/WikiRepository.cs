using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Icklewik.Core
{
    public class WikiRepositoryEventArgs : EventArgs
    {
        // used for "move" events only
        public string OldWikiPath { get; set; }

        // relative path to source file
        public string MarkdownPath { get; set; }

        // relative path to wiki page (where it's stored on the file system)
        public string WikiPath { get; set; }

        // relative url to wiki page
        public string WikiUrl { get; set; }
    }

    // make sure we compare paths in a consistent way
    public class WikiPathComparer : IEqualityComparer<String>
    {
        public bool Equals(string x, string y)
        {
            return x.Equals(y, StringComparison.InvariantCultureIgnoreCase);
        }

        public int GetHashCode(string obj)
        {
            return obj.GetHashCode();
        }
    }

    /// <summary>
    /// This is the underlying data model for a wiki site, it contains details of all directories and pages
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
                return rootDirectory.MarkdownPath;
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

    /// <summary>
    /// The repository stores the logical representation of the source files and maps the source files to the
    /// corresponding wiki pages
    /// 
    /// Changes to the repository can be tracked using the relevant events
    /// </summary>
    public class WikiRepository
    {
        // small config class, used to simplify the "AddDirectory" method
        private class RootInfo
        {
            public string RootWikiPath { get; set; }
        }

        private string fileExtension;

        // core model, this represents the current state of the wiki
        private WikiModel wikiModel;

        private DeleteVisitor deleteVisitor;

        public WikiRepository(string extension)
        {
            fileExtension = extension;

            wikiModel = new WikiModel();

            deleteVisitor = new DeleteVisitor(
                page => DeletePageDetails(page),
                dir => DeleteDirectoryDetails(dir)
            );
        }

        public event Action<object, WikiRepositoryEventArgs> PageAdded;
        public event Action<object, WikiRepositoryEventArgs> PageUpdated;
        public event Action<object, WikiRepositoryEventArgs> PageDeleted;
        public event Action<object, WikiRepositoryEventArgs> PageMoved;

        public event Action<object, WikiRepositoryEventArgs> DirectoryAdded;
        public event Action<object, WikiRepositoryEventArgs> DirectoryUpdated;
        public event Action<object, WikiRepositoryEventArgs> DirectoryDeleted;
        public event Action<object, WikiRepositoryEventArgs> DirectoryMoved;

        public void Init(string rootSourcePath, string rootWikiPath, IEnumerable<string> initialFiles)
        {
            // create the root directory as as special case
            AddDirectory(GetFullPath(rootSourcePath), new RootInfo { RootWikiPath = GetFullPath(rootWikiPath) });

            // iterate over all directories and add all relevant files to the model (which will fire the relevant events and prompt the
            // generator to generate the wiki itself)
            foreach (string markdownFilePath in initialFiles.Select(path => GetFullPath(path)))
            {
                if (!wikiModel.PathComparer.Equals(markdownFilePath, wikiModel.RootSourcePath))
                {
                    AddPage(markdownFilePath);
                }
            }
        }

        public void AddPage(string fullPath)
        {
            // make sure we've got the full path
            fullPath = GetFullPath(fullPath);

            Debug.Assert(!wikiModel.ContainsAsset(fullPath));

            string parentPath = Path.GetDirectoryName(fullPath);

            // recursively add any directories between us and the closest ancestor
            // in the map
            if (!wikiModel.ContainsAsset(parentPath))
            {
                AddDirectory(parentPath, null);
            }

            WikiDirectory parent = wikiModel.GetAsset(parentPath) as WikiDirectory;

            WikiPage newPage = new WikiPage
            {
                MarkdownPath = fullPath,
                LastUpdated = DateTime.UtcNow,
                Depth = fullPath.Count(c => c.Equals(Path.DirectorySeparatorChar)),
                Parent = parent
            };

            string relativePath = GetWikiPath(newPage.MarkdownPath);

            // change file extension
            newPage.WikiUrl = Path.Combine(Path.GetDirectoryName(relativePath), Path.GetFileNameWithoutExtension(relativePath) + ".html");
            newPage.WikiPath = Path.Combine(wikiModel.RootWikiPath, newPage.WikiUrl);

            // add child to the parent
            parent.Children.Add(newPage);

            // add to the map
            wikiModel.AddAsset(fullPath, newPage);

            // fire event
            FireEvent(PageAdded, newPage);
        }

        public void UpdatePage(string fullPath)
        {
            // make sure we've got the full path
            fullPath = GetFullPath(fullPath);

            Debug.Assert(wikiModel.ContainsAsset(fullPath));

            WikiEntry entry;
            if (wikiModel.TryGetAsset(fullPath, out entry))
            {
                entry.LastUpdated = DateTime.UtcNow;

                // fire event
                FireEvent(PageUpdated, entry);
            }
        }

        public void DeletePage(string fullPath)
        {            
            // make sure we've got the full path
            fullPath = GetFullPath(fullPath);

            Debug.Assert(wikiModel.ContainsAsset(fullPath));

            WikiEntry entry;
            if (wikiModel.TryGetAsset(fullPath, out entry))
            {
                DeleteEntryCommon(entry);
            }
        }

        public void RenamePage(string oldFullPath, string newFullPath)
        {
            // make sure we've got the full paths
            oldFullPath = GetFullPath(oldFullPath);
            newFullPath = GetFullPath(newFullPath);

            // it's possible that the file rename could bring the file
            // into scope (i.e. if the file extension changes)
            // in that case this is just an add

            WikiEntry entry;
            if (wikiModel.TryGetAsset(oldFullPath, out entry))
            {
                if (newFullPath.EndsWith(fileExtension))
                {
                    var visitor = new RenameVisitor(
                        oldFullPath,
                        newFullPath,
                        (oldWikiPath, page) =>
                        {
                            FireEvent(PageMoved, page, oldWikiPath);
                        });

                    entry.Accept(visitor);
                }
                else
                {
                    // we've changed to a non-relevant extension, delete
                    DeletePage(oldFullPath);
                }
            }
            else if (Path.GetExtension(newFullPath).Equals(fileExtension, StringComparison.InvariantCultureIgnoreCase))
            {
                // and its only an add if the extension is correct (just in case we have
                // been renamed from (for example) .md to .txt
                AddPage(newFullPath);
            }
        }

        public void AddDirectory(string fullPath)
        {
            // make sure we've got the full paths
            //fullPath = GetFullPath(fullPath);

            //Debug.Assert(!wikiEntryMap.ContainsKey(fullPath));

            //AddDirectory(fullPath, null);
        }

        public void UpdateDirectory(string fullPath)
        {
            // make sure we've got the full paths
            fullPath = GetFullPath(fullPath);

            Debug.Assert(wikiModel.ContainsAsset(fullPath));

            WikiEntry entry;
            if (wikiModel.TryGetAsset(fullPath, out entry))
            {
                entry.LastUpdated = DateTime.UtcNow;

                // fire event
                FireEvent(DirectoryUpdated, entry);
            }
        }

        public void DeleteDirectory(string fullPath)
        {
            // make sure we've got the full paths
            fullPath = GetFullPath(fullPath);

            WikiEntry entry;
            if (wikiModel.TryGetAsset(fullPath, out entry))
            {
                DeleteEntryCommon(entry);
            }
        }

        public void RenameDirectory(string oldFullPath, string newFullPath)
        {            
            // make sure we've got the full paths
            oldFullPath = GetFullPath(oldFullPath);
            newFullPath = GetFullPath(newFullPath);

            WikiEntry entry;
            if (wikiModel.TryGetAsset(oldFullPath, out entry))
            {
                var visitor = new RenameVisitor(
                    oldFullPath,
                    newFullPath,
                    (oldWikiPath, page) =>
                    {
                        FireEvent(PageMoved, page, oldWikiPath);
                    },
                    (oldWikiPath, directory) =>
                    {
                        FireEvent(DirectoryMoved, directory, oldWikiPath);
                    });

                entry.Accept(visitor);
            }
        }

        /// <summary>
        /// Adds a directory to the model.
        /// </summary>
        /// <param name="sourcePath">Source path of the directory</param>
        /// <param name="rootInfo">Contains information about directory, *only* if the directory being added is the root</param>
        private void AddDirectory(string sourcePath, RootInfo rootInfo)
        {
            WikiDirectory parent = null;
            if (rootInfo == null)
            {
                string parentPath = Path.GetDirectoryName(sourcePath);

                // recursively add any directories between us and the closest ancestor
                // in the map
                if (!wikiModel.ContainsAsset(parentPath))
                {
                    AddDirectory(parentPath, null);
                }

                parent = wikiModel.GetAsset(parentPath) as WikiDirectory;
            }

            WikiDirectory newDirectory = new WikiDirectory
            {
                MarkdownPath = sourcePath,
                LastUpdated = DateTime.UtcNow,
                Depth = sourcePath.Count(c => c.Equals(Path.DirectorySeparatorChar)),
                Parent = parent,
                Children = new List<WikiEntry>()
            };

            if (rootInfo == null)
            {
                newDirectory.WikiUrl = GetWikiPath(newDirectory.MarkdownPath);
                newDirectory.WikiPath = Path.Combine(wikiModel.RootWikiPath, newDirectory.WikiUrl);

                // add child to the parent
                parent.Children.Add(newDirectory);
            }
            else
            {
                newDirectory.WikiUrl = "";
                newDirectory.WikiPath = rootInfo.RootWikiPath;

                wikiModel.SetRootDirectory(newDirectory);
            }

            wikiModel.AddAsset(newDirectory.MarkdownPath, newDirectory);

            // fire event
            FireEvent(DirectoryAdded, newDirectory);
        }

        private void DeleteEntryCommon(WikiEntry entry)
        {
            // delete all of our children
            entry.Accept(deleteVisitor);

            // remove from our parent
            if (entry.Parent != null)
            {
                entry.Parent.Children.Remove(entry);

                // now make sure that any empty directories are tidied up
                DeleteParentIfEmpty(entry.Parent);
            }
        }

        private void DeleteParentIfEmpty(WikiDirectory parent)
        {
            // there's no point keeping empty directories around so delete if empty
            if (!parent.Children.Any())
            {
                DeleteEntryCommon(parent);
            }
        }

        private void DeletePageDetails(WikiPage page)
        {
            wikiModel.RemoveAsset(page.MarkdownPath);

            // fire event
            FireEvent(PageDeleted, page );
        }

        private void DeleteDirectoryDetails(WikiDirectory directory)
        {
            // remove ourselves from the map
            wikiModel.RemoveAsset(directory.MarkdownPath);

            // fire event
            FireEvent(DirectoryDeleted, directory );
        }

        /// <summary>
        /// Generates the canonical full path, use for all further comparisons, indices etc
        /// </summary>
        /// <param name="path">Path, can be relative (to the working directory) or a full path. File or directory</param>
        /// <returns>Canonical full path</returns>
        private string GetFullPath(string path)
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Generates the wiki path (that is the relative path, based on the wiki's root)
        /// </summary>
        /// <param name="fullPath">Canonical full path, generated by GetFullPath (above)</param>
        /// <returns>Wiki's relative path</returns>
        private string GetWikiPath(string fullPath)
        {
            return fullPath.Substring(wikiModel.RootSourcePath.Length + 1);
        }
         
        private void FireEvent(Action<object, WikiRepositoryEventArgs> eventToFire, WikiEntry entry, string oldWikiPath = "")
        {
            if (eventToFire != null)
            {
                eventToFire(this, new WikiRepositoryEventArgs
                    {
                        MarkdownPath = entry.MarkdownPath,
                        WikiPath = entry.WikiPath,
                        WikiUrl = entry.WikiUrl,
                        OldWikiPath = oldWikiPath
                    });
            }
        }
    }
}
