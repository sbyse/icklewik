using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Icklewik.Core.Model
{
    public class WikiRepositoryEventArgs : EventArgs
    {
        public WikiRepositoryEventArgs(
            string oldSourcePath,
            string oldWikiPath,
            string sourcePath,
            string wikiPath,
            string wikiUrl)
        {
            OldSourcePath = oldSourcePath;
            OldWikiPath = oldWikiPath;
            SourcePath = sourcePath;
            WikiPath = wikiPath;
            WikiUrl = wikiUrl;
        }

        // used for "move" events only
        public string OldSourcePath { get; private set; }

        // used for "move" events only
        public string OldWikiPath { get; private set; }

        // relative path to source file
        public string SourcePath { get; private set; }

        // relative path to wiki page (where it's stored on the file system)
        public string WikiPath { get; private set; }

        // relative url to wiki page
        public string WikiUrl { get; private set; }
    }

    public interface ILockPolicy
    {
        IDisposable GetScopedLock();
    }

    public class NullLockPolicy : ILockPolicy
    {
        private class NullLock : IDisposable
        {
            public void Dispose()
            {
 	            // do nothing
            }
        }

        public IDisposable GetScopedLock()
        {
            return new NullLock();
        }
    }

    public class ThreadSafeLockPolicy : ILockPolicy
    {
        private class ScopedLock : IDisposable
        {
            private object locker;
            
            public ScopedLock()
            {
                locker = new object();

                Monitor.Enter(locker);
            }

            public void Dispose()
            {
                if (locker != null)
                {
                    Monitor.Exit(locker);
                    locker = null;
                }
            }
        }

        public IDisposable GetScopedLock()
        {
            return new ScopedLock();
        }
    }

    /// <summary>
    /// The repository stores the logical representation of the source files and maps the source files to the
    /// corresponding wiki pages
    /// </summary>
    public abstract class WikiRepository<TLockPolicy> where TLockPolicy : ILockPolicy, new()
    {
        // small config class, used to simplify the "AddDirectory" method
        protected class RootInfo
        {
            public string RootWikiPath { get; set; }
        }
        
        private TLockPolicy lockPolicy;

        private string fileExtension;

        private DeleteVisitor deleteVisitor;

        public WikiRepository(string extension)
        {
            lockPolicy = new TLockPolicy();

            fileExtension = extension;

            UnderlyingModel = new WikiModel();

            deleteVisitor = new DeleteVisitor(
                page => DeletePageDetails(page),
                dir => DeleteDirectoryDetails(dir)
            );
        }

        /// <summary>
        /// Represents the file system location at the top of the source tree
        /// </summary>
        public string RootSourcePath
        {
            get
            {
                return UnderlyingModel.RootSourcePath;
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
                return UnderlyingModel.RootWikiPath;
            }
        }

        // TODO: Need this to be made optionally available and protected
        // model, this represents the current state of the wiki, use with care
        protected WikiModel UnderlyingModel { get; private set; }

        public void AddPage(string fullPath)
        {
            // make sure we've got the full path
            fullPath = PathHelper.GetFullPath(fullPath);

            Debug.Assert(!UnderlyingModel.ContainsAsset(fullPath));

            string parentPath = Path.GetDirectoryName(fullPath);

            // recursively add any directories between us and the closest ancestor
            // in the map
            if (!UnderlyingModel.ContainsAsset(parentPath))
            {
                AddDirectory(parentPath, null);
            }

            WikiDirectory parent = UnderlyingModel.GetAsset(parentPath) as WikiDirectory;

            WikiPage newPage = new WikiPage
            {
                SourcePath = fullPath,
                LastUpdated = DateTime.UtcNow,
                Depth = fullPath.Count(c => c.Equals(Path.DirectorySeparatorChar)),
                Parent = parent
            };

            string relativePath = GetWikiPath(newPage.SourcePath);

            // change file extension
            newPage.WikiUrl = Path.Combine(Path.GetDirectoryName(relativePath), Path.GetFileNameWithoutExtension(relativePath) + ".html");
            newPage.WikiPath = Path.Combine(UnderlyingModel.RootWikiPath, newPage.WikiUrl);

            // add child to the parent
            parent.Children.Add(newPage);

            // add to the map
            UnderlyingModel.AddAsset(fullPath, newPage);

            // fire event
            HandlePageAdded(newPage);
        }

        public void UpdatePage(string fullPath)
        {
            // make sure we've got the full path
            fullPath = PathHelper.GetFullPath(fullPath);

            Debug.Assert(UnderlyingModel.ContainsAsset(fullPath));

            WikiEntry entry;
            if (UnderlyingModel.TryGetAsset(fullPath, out entry))
            {
                entry.LastUpdated = DateTime.UtcNow;

                // fire event
                HandlePageUpdated(entry as WikiPage);
            }
        }

        public void DeletePage(string fullPath)
        {            
            // make sure we've got the full path
            fullPath = PathHelper.GetFullPath(fullPath);

            Debug.Assert(UnderlyingModel.ContainsAsset(fullPath));

            WikiEntry entry;
            if (UnderlyingModel.TryGetAsset(fullPath, out entry))
            {
                DeleteEntryCommon(entry);
            }
        }

        public void RenamePage(string oldFullPath, string newFullPath)
        {
            // make sure we've got the full paths
            oldFullPath = PathHelper.GetFullPath(oldFullPath);
            newFullPath = PathHelper.GetFullPath(newFullPath);

            // it's possible that the file rename could bring the file
            // into scope (i.e. if the file extension changes)
            // in that case this is just an add

            WikiEntry entry;
            if (UnderlyingModel.TryGetAsset(oldFullPath, out entry))
            {
                if (newFullPath.EndsWith(fileExtension))
                {
                    var visitor = new RenameVisitor(
                        oldFullPath,
                        newFullPath,
                        (oldSourcePath, oldWikiPath, page) =>
                        {
                            HandlePageMoved(page, oldSourcePath, oldWikiPath);
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
            // NOTE: Adding a directory does nothing, we are not interested in empty
            // directories, only the files they contain

            // make sure we've got the full paths
            //fullPath = PathHelper.GetFullPath(fullPath);

            //Debug.Assert(!UnderlyingModel.ContainsAsset(fullPath));

            //AddDirectory(fullPath, null);
        }

        public void UpdateDirectory(string fullPath)
        {
            // make sure we've got the full paths
            fullPath = PathHelper.GetFullPath(fullPath);

            Debug.Assert(UnderlyingModel.ContainsAsset(fullPath));

            WikiEntry entry;
            if (UnderlyingModel.TryGetAsset(fullPath, out entry))
            {
                entry.LastUpdated = DateTime.UtcNow;

                // fire event
                HandleDirectoryUpdated(entry as WikiDirectory);
            }
        }

        public void DeleteDirectory(string fullPath)
        {
            // make sure we've got the full paths
            fullPath = PathHelper.GetFullPath(fullPath);

            WikiEntry entry;
            if (UnderlyingModel.TryGetAsset(fullPath, out entry))
            {
                DeleteEntryCommon(entry);
            }
        }

        public void RenameDirectory(string oldFullPath, string newFullPath)
        {            
            // make sure we've got the full paths
            oldFullPath = PathHelper.GetFullPath(oldFullPath);
            newFullPath = PathHelper.GetFullPath(newFullPath);

            WikiEntry entry;
            if (UnderlyingModel.TryGetAsset(oldFullPath, out entry))
            {
                var visitor = new RenameVisitor(
                    oldFullPath,
                    newFullPath,
                    (oldSourcePath, oldWikiPath, page) =>
                    {
                        HandlePageMoved(page, oldSourcePath, oldWikiPath);
                    },
                    (oldSourcePath, oldWikiPath, directory) =>
                    {
                        HandleDirectoryMoved(directory, oldSourcePath, oldWikiPath);
                    });

                entry.Accept(visitor);
            }
        }

        //
        // Abstract methods dictate how to handle "events"
        //

        protected abstract void HandlePageAdded(WikiPage page);
        protected abstract void HandlePageUpdated(WikiPage page);
        protected abstract void HandlePageDeleted(WikiPage page);
        protected abstract void HandlePageMoved(WikiPage page, string oldSourcePath, string oldWikiPath);

        protected abstract void HandleDirectoryAdded(WikiDirectory page);
        protected abstract void HandleDirectoryUpdated(WikiDirectory page);
        protected abstract void HandleDirectoryDeleted(WikiDirectory page);
        protected abstract void HandleDirectoryMoved(WikiDirectory page, string oldSourcePath, string oldWikiPath);

        /// <summary>
        /// Adds a directory to the model.
        /// </summary>
        /// <param name="sourcePath">Source path of the directory</param>
        /// <param name="rootInfo">Contains information about directory, *only* if the directory being added is the root</param>
        protected void AddDirectory(string sourcePath, RootInfo rootInfo)
        {
            WikiDirectory parent = null;
            if (rootInfo == null)
            {
                string parentPath = Path.GetDirectoryName(sourcePath);

                // recursively add any directories between us and the closest ancestor
                // in the map
                if (!UnderlyingModel.ContainsAsset(parentPath))
                {
                    AddDirectory(parentPath, null);
                }

                parent = UnderlyingModel.GetAsset(parentPath) as WikiDirectory;
            }

            WikiDirectory newDirectory = new WikiDirectory
            {
                SourcePath = sourcePath,
                LastUpdated = DateTime.UtcNow,
                Depth = sourcePath.Count(c => c.Equals(Path.DirectorySeparatorChar)),
                Parent = parent,
                Children = new List<WikiEntry>()
            };

            if (rootInfo == null)
            {
                newDirectory.WikiUrl = GetWikiPath(newDirectory.SourcePath);
                newDirectory.WikiPath = Path.Combine(UnderlyingModel.RootWikiPath, newDirectory.WikiUrl);

                // add child to the parent
                parent.Children.Add(newDirectory);
            }
            else
            {
                newDirectory.WikiUrl = "";
                newDirectory.WikiPath = rootInfo.RootWikiPath;

                UnderlyingModel.SetRootDirectory(newDirectory);
            }

            UnderlyingModel.AddAsset(newDirectory.SourcePath, newDirectory);

            // fire event
            HandleDirectoryAdded(newDirectory);
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
            UnderlyingModel.RemoveAsset(page.SourcePath);

            // fire event
            HandlePageDeleted(page);
        }

        private void DeleteDirectoryDetails(WikiDirectory directory)
        {
            // remove ourselves from the map
            UnderlyingModel.RemoveAsset(directory.SourcePath);

            // fire event
            HandleDirectoryDeleted(directory);
        }

        /// <summary>
        /// Generates the wiki path (that is the relative path, based on the wiki's root)
        /// </summary>
        /// <param name="fullPath">Canonical full path, generated by PathHelper.GetFullPath</param>
        /// <returns>Wiki's relative path</returns>
        private string GetWikiPath(string fullPath)
        {
            return fullPath.Substring(UnderlyingModel.RootSourcePath.Length + 1);
        }
    }
}
