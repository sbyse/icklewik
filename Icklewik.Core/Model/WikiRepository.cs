using Icklewik.Core.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Icklewik.Core.Model
{
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

        //
        // Abstract methods dictate how to handle "model events"
        //

        protected abstract void HandlePageAdded(WikiPage page);
        protected abstract void HandlePageUpdated(WikiPage page);
        protected abstract void HandlePageDeleted(WikiPage page);
        protected abstract void HandlePageMoved(WikiPage page, string oldSourcePath, string oldWikiPath, string oldWikiUrl);

        protected abstract void HandleDirectoryAdded(WikiDirectory page);
        protected abstract void HandleDirectoryUpdated(WikiDirectory page);
        protected abstract void HandleDirectoryDeleted(WikiDirectory page);
        protected abstract void HandleDirectoryMoved(WikiDirectory page, string oldSourcePath, string oldWikiPath, string oldWikiUrl);

        //
        // Protected methods, for use by derived classes
        //

        protected void AddPage(string fullPath)
        {
            // make sure we've got the full path
            fullPath = PathHelper.GetFullPath(fullPath);

            if (UnderlyingModel.ContainsAssetBySourcePath(fullPath))
            {
                // page already exists
                return;
            }

            string parentPath = Path.GetDirectoryName(fullPath);

            // recursively add any directories between us and the closest ancestor
            // in the map
            if (!UnderlyingModel.ContainsAssetBySourcePath(parentPath))
            {
                AddDirectory(parentPath, null);
            }

            WikiDirectory parent = UnderlyingModel.UnsafeGetAsset(parentPath) as WikiDirectory;

            WikiPage newPage = new WikiPage
            {
                SourcePath = fullPath,
                LastUpdated = DateTime.UtcNow,
                Depth = fullPath.Count(c => c.Equals(Path.DirectorySeparatorChar)),
                Parent = parent
            };

            string relativePath = PathHelper.GetWikiUrl(UnderlyingModel.RootSourcePath, newPage.SourcePath);

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

        protected void UpdatePage(string fullPath)
        {
            // make sure we've got the full path
            fullPath = PathHelper.GetFullPath(fullPath);

            if (!UnderlyingModel.ContainsAssetBySourcePath(fullPath))
            {
                // page doesn't exist
                return;
            }

            WikiEntry entry;
            if (UnderlyingModel.UnsafeTryGetAssetBySourcePath(fullPath, out entry))
            {
                entry.LastUpdated = DateTime.UtcNow;

                // fire event
                HandlePageUpdated(entry as WikiPage);
            }
        }

        protected void DeletePage(string fullPath)
        {
            Log.Instance.Log(string.Format("Deleting page {0}", fullPath));

            // make sure we've got the full path
            fullPath = PathHelper.GetFullPath(fullPath);

            WikiEntry entry;
            if (UnderlyingModel.UnsafeTryGetAssetBySourcePath(fullPath, out entry))
            {
                DeleteEntryCommon(entry);
            }
        }

        protected void RenamePage(string oldFullPath, string newFullPath)
        {
            // make sure we've got the full paths
            oldFullPath = PathHelper.GetFullPath(oldFullPath);
            newFullPath = PathHelper.GetFullPath(newFullPath);

            // it's possible that the file rename could bring the file
            // into scope (i.e. if the file extension changes)
            // in that case this is just an add

            WikiEntry entry;
            if (UnderlyingModel.UnsafeTryGetAssetBySourcePath(oldFullPath, out entry))
            {
                if (newFullPath.EndsWith(fileExtension))
                {
                    var visitor = new RenameVisitor(
                        oldFullPath,
                        newFullPath,
                        (oldSourcePath, oldWikiPath, oldWikiUrl, page) =>
                        {
                            HandlePageMoved(page, oldSourcePath, oldWikiPath, oldWikiUrl);
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

        protected void AddDirectory(string fullPath)
        {
            // NOTE: Adding a directory does nothing, we are not interested in empty
            // directories, only the files they contain

            // make sure we've got the full paths
            //fullPath = PathHelper.GetFullPath(fullPath);

            //Debug.Assert(!UnderlyingModel.ContainsAsset(fullPath));

            //AddDirectory(fullPath, null);
        }

        protected void UpdateDirectory(string fullPath)
        {
            // make sure we've got the full paths
            fullPath = PathHelper.GetFullPath(fullPath);

            WikiEntry entry;
            if (UnderlyingModel.UnsafeTryGetAssetBySourcePath(fullPath, out entry))
            {
                entry.LastUpdated = DateTime.UtcNow;

                // fire event
                HandleDirectoryUpdated(entry as WikiDirectory);
            }
        }

        protected void DeleteDirectory(string fullPath)
        {
            Log.Instance.Log(string.Format("Deleting directory {0}", fullPath));

            // make sure we've got the full paths
            fullPath = PathHelper.GetFullPath(fullPath);

            WikiEntry entry;
            if (UnderlyingModel.UnsafeTryGetAssetBySourcePath(fullPath, out entry))
            {
                DeleteEntryCommon(entry);
            }
        }

        protected void RenameDirectory(string oldFullPath, string newFullPath)
        {            
            // make sure we've got the full paths
            oldFullPath = PathHelper.GetFullPath(oldFullPath);
            newFullPath = PathHelper.GetFullPath(newFullPath);

            WikiEntry entry;
            if (UnderlyingModel.UnsafeTryGetAssetBySourcePath(oldFullPath, out entry))
            {
                var visitor = new RenameVisitor(
                    oldFullPath,
                    newFullPath,
                    (oldSourcePath, oldWikiPath, oldWikiUrl, page) =>
                    {
                        HandlePageMoved(page, oldSourcePath, oldWikiPath, oldWikiUrl);
                    },
                    (oldSourcePath, oldWikiPath, oldWikiUrl, directory) =>
                    {
                        HandleDirectoryMoved(directory, oldSourcePath, oldWikiPath, oldWikiUrl);
                    });

                entry.Accept(visitor);
            }
        }

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
                if (!UnderlyingModel.ContainsAssetBySourcePath(parentPath))
                {
                    AddDirectory(parentPath, null);
                }

                parent = UnderlyingModel.UnsafeGetAsset(parentPath) as WikiDirectory;
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
                newDirectory.WikiUrl = PathHelper.GetWikiUrl(UnderlyingModel.RootSourcePath, newDirectory.SourcePath);
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
    }
}
