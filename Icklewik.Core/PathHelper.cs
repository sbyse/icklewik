using System.IO;

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
    }
}
