﻿using System.IO;

namespace Icklewik.Core
{
    public static class PathHelper
    {
        /// <summary>
        /// Generates the canonical full path, use for all further comparisons, indices etc
        /// </summary>
        /// <param name="paths">Relative paths to be combined</param>
        /// <returns>Canonical full path</returns>
        public static string GetFullPath(params string[] paths)
        {
            return GetFullPath(Path.Combine(paths));
        }

        /// <summary>
        /// Generates the canonical full path, use for all further comparisons, indices etc
        /// </summary>
        /// <param name="path">Path, can be relative (to the working directory) or a full path. File or directory</param>
        /// <returns>Canonical full path</returns>
        public static string GetFullPath(string path)
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Generates the wiki path (that is the relative path, based on the wiki's root)
        /// </summary>
        /// <param name="fullPath">Full path</param>
        /// <returns>Wiki's relative path</returns>
        public static string GetWikiUrl(string rootPath, string fullPath)
        {
            return PathHelper.GetFullPath(fullPath).Substring(rootPath.Length).TrimStart(Path.DirectorySeparatorChar);
        }
    }
}
