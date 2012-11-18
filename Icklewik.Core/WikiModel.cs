using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Icklewik.Core
{
    /// <summary>
    /// The model stores the logical representation of the source files and maps the source files to the
    /// corresponding wiki pages
    /// 
    /// Changes to the model can be tracked using the relevant events
    /// </summary>
    public class WikiModel
    {
        private string fileExtension;

        // root directory represents the base node of the source tree, the tree can
        // be traversed from this point if required
        private WikiDirectory rootDirectory;

        // we also keep all entries in a map for simple retrieval of specific entries
        private IDictionary<string, WikiEntry> wikiEntryMap;

        private DeleteVisitor deleteVisitor;

        // small config class, used to simplify the "AddDirectory" method
        private class RootInfo
        {
            public string RootWikiPath { get; set; }
        }

        public WikiModel(string extension)
        {
            fileExtension = extension;

            wikiEntryMap = new Dictionary<string, WikiEntry>();

            deleteVisitor = new DeleteVisitor(
                page => DeletePageDetails(page),
                dir => DeleteDirectoryDetails(dir)
            );
        }

        public event Action<WikiPage> PageAdded;
        public event Action<WikiPage> PageUpdated;
        public event Action<WikiPage> PageDeleted;
        public event Action<string, WikiPage> PageMoved;

        public event Action<WikiDirectory> DirectoryAdded;
        public event Action<WikiDirectory> DirectoryUpdated;
        public event Action<WikiDirectory> DirectoryDeleted;
        public event Action<string, WikiDirectory> DirectoryMoved;

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

        public void Init(string rootSourcePath, string rootWikiPath, IEnumerable<string> initialFiles)
        {
            // create the root directory as as special case
            AddDirectory(rootSourcePath, new RootInfo { RootWikiPath = rootWikiPath });

            // iterate over all directories and add all relevant files to the model (which will fire the relevant events and prompt the
            // generator to generate the wiki itself)
            foreach (string markdownFilePath in initialFiles)
            {
                if (!markdownFilePath.Equals(rootSourcePath))
                {
                    AddPage(markdownFilePath);
                }
            }
        }

        public void AddPage(string fullPath)
        {
            Debug.Assert(!wikiEntryMap.ContainsKey(fullPath));

            string parentPath = Path.GetDirectoryName(fullPath);

            // recursively add any directories between us and the closest ancestor
            // in the map
            if (!wikiEntryMap.ContainsKey(parentPath))
            {
                AddDirectory(parentPath, null);
            }

            WikiDirectory parent = wikiEntryMap[parentPath] as WikiDirectory;

            WikiPage newPage = new WikiPage
            {
                MarkdownPath = fullPath,
                LastUpdated = DateTime.UtcNow,
                Depth = fullPath.Count(c => c.Equals(Path.DirectorySeparatorChar)),
                Parent = parent
            };

            string relativePath = newPage.MarkdownPath.Substring(rootDirectory.MarkdownPath.Length - 1);

            // change file extension
            newPage.WikiUrl = Path.Combine(Path.GetDirectoryName(relativePath), Path.GetFileNameWithoutExtension(relativePath) + ".html");
            newPage.WikiPath = Path.Combine(rootDirectory.WikiPath, newPage.WikiUrl);

            // add child to the parent
            parent.Children.Add(newPage);

            // add to the map
            wikiEntryMap[fullPath] = newPage;

            if (PageAdded != null)
            {
                PageAdded(newPage);
            }
        }

        public void UpdatePage(string fullPath)
        {
            Debug.Assert(wikiEntryMap.ContainsKey(fullPath));

            WikiEntry entry;
            if (wikiEntryMap.TryGetValue(fullPath, out entry))
            {
                entry.LastUpdated = DateTime.UtcNow;

                if (PageUpdated != null)
                {
                    PageUpdated(entry as WikiPage);
                }
            }
        }

        public void DeletePage(string fullPath)
        {
            Debug.Assert(wikiEntryMap.ContainsKey(fullPath));

            WikiEntry entry;
            if (wikiEntryMap.TryGetValue(fullPath, out entry))
            {
                DeleteEntryCommon(entry);
            }
        }

        public void RenamePage(string oldFullPath, string newFullPath)
        {
            // it's possible that the file rename could bring the file
            // into scope (i.e. if the file extension changes)
            // in that case this is just an add

            WikiEntry entry;
            if (wikiEntryMap.TryGetValue(oldFullPath, out entry))
            {
                if (newFullPath.Contains(fileExtension))
                {
                    var visitor = new RenameVisitor(
                        oldFullPath,
                        newFullPath,
                        (oldWikiPath, page) =>
                        {
                            if (PageMoved != null)
                            {
                                PageMoved(oldWikiPath, page);
                            }
                        });

                    entry.Accept(visitor);
                }
                else
                {
                    // we've changed to a non-relevant extension, delete
                    DeletePage(oldFullPath);
                }
            }
            else if (Path.GetExtension(newFullPath).Equals(fileExtension))
            {
                // and its only an add if the extension is correct (just in case we have
                // been renamed from (for example) .md to .txt
                AddPage(newFullPath);
            }
        }

        public void AddDirectory(string fullPath)
        {
            //Debug.Assert(!wikiEntryMap.ContainsKey(fullPath));

            //AddDirectory(fullPath, null);
        }

        public void UpdateDirectory(string fullPath)
        {
            Debug.Assert(wikiEntryMap.ContainsKey(fullPath));

            WikiEntry entry;
            if (wikiEntryMap.TryGetValue(fullPath, out entry))
            {
                entry.LastUpdated = DateTime.UtcNow;

                if (DirectoryUpdated != null)
                {
                    DirectoryUpdated(entry as WikiDirectory);
                }
            }
        }

        public void DeleteDirectory(string fullPath)
        {
            // this will be called when a directory is deleted,
            // it should therefore delete all child entries
            Debug.Assert(wikiEntryMap.ContainsKey(fullPath));

            WikiEntry entry;
            if (wikiEntryMap.TryGetValue(fullPath, out entry))
            {
                DeleteEntryCommon(entry);
            }
        }

        public void RenameDirectory(string oldFullPath, string newFullPath)
        {
            WikiEntry entry;
            if (wikiEntryMap.TryGetValue(oldFullPath, out entry))
            {
                var visitor = new RenameVisitor(
                    oldFullPath,
                    newFullPath,
                    (oldWikiPath, page) =>
                    {
                        if (PageMoved != null)
                        {
                            PageMoved(oldWikiPath, page);
                        }
                    },
                    (oldWikiPath, directory) =>
                    {
                        if (DirectoryMoved != null)
                        {
                            DirectoryMoved(oldWikiPath, directory);
                        }
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
                if (!wikiEntryMap.ContainsKey(parentPath))
                {
                    AddDirectory(parentPath, null);
                }

                parent = wikiEntryMap[parentPath] as WikiDirectory;
            }

            WikiDirectory newDirectory = new WikiDirectory
            {
                MarkdownPath = sourcePath,
                LastUpdated = DateTime.UtcNow,
                LastGenerated = null, // set when we generate the relevant directory
                Depth = sourcePath.Count(c => c.Equals(Path.DirectorySeparatorChar)),
                Parent = parent,
                Children = new List<WikiEntry>()
            };

            if (rootInfo == null)
            {
                newDirectory.WikiUrl = newDirectory.MarkdownPath.Substring(rootDirectory.MarkdownPath.Length - 1);
                newDirectory.WikiPath = Path.Combine(rootDirectory.WikiPath, newDirectory.WikiUrl);

                // add child to the parent
                parent.Children.Add(newDirectory);
            }
            else
            {
                newDirectory.WikiUrl = "";
                newDirectory.WikiPath = rootInfo.RootWikiPath;

                rootDirectory = newDirectory;
            }

            wikiEntryMap[newDirectory.MarkdownPath] = newDirectory;

            // fire event
            if (DirectoryAdded != null)
            {
                DirectoryAdded(newDirectory);
            }
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
            wikiEntryMap.Remove(page.MarkdownPath);

            // fire event
            if (PageDeleted != null)
            {
                PageDeleted(page);
            }
        }

        private void DeleteDirectoryDetails(WikiDirectory directory)
        {
            // remove ourselves from the map
            wikiEntryMap.Remove(directory.MarkdownPath);

            // fire event
            if (DirectoryDeleted != null)
            {
                DirectoryDeleted(directory);
            }
        }
    }
}
