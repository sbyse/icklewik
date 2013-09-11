using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Icklewik.Core.Model
{
    public interface IWikiEntry
    {
        // relative path to source file
        string SourcePath { get; }

        // relative path to wiki page (where it's stored on the file system)
        string WikiPath { get; }

        // relative url to wiki page
        string WikiUrl { get; }

        // time that a wiki entry was last updated
        DateTime LastUpdated { get; }

        // how many levels into the structure are we?
        int Depth { get; }
    }

    public class ImmutableWikiEntry : IWikiEntry
    {
        public ImmutableWikiEntry(
            string sourcePath,
            string wikiPath,
            string wikiUrl,
            DateTime lastUpdated,
            int depth)
        {
            SourcePath = sourcePath;
            WikiPath = wikiPath;
            WikiUrl = wikiUrl;
            LastUpdated = lastUpdated;
            Depth = depth;
        }

        // relative path to source file
        public string SourcePath { get; private set; }

        // relative path to wiki page (where it's stored on the file system)
        public string WikiPath { get; private set; }

        // relative url to wiki page
        public string WikiUrl { get; private set; }

        // time that a wiki entry was last updated
        public DateTime LastUpdated { get; private set; }

        // how many levels into the structure are we?
        public int Depth { get; private set; }
    }

    public class ImmutableWikiPage : ImmutableWikiEntry
    {
        public ImmutableWikiPage(
            string sourcePath,
            string wikiPath,
            string wikiUrl,
            DateTime lastUpdated,
            int depth)
            : base(
                sourcePath,
                wikiPath,
                wikiUrl,
                lastUpdated,
                depth)
        {
        }
    }

    public class ImmutableWikiDirectory : ImmutableWikiEntry
    {
        public ImmutableWikiDirectory(
            string sourcePath,
            string wikiPath,
            string wikiUrl,
            DateTime lastUpdated,
            int depth,
            IEnumerable<ImmutableWikiEntry> children)
            : base(
                sourcePath,
                wikiPath,
                wikiUrl,
                lastUpdated,
                depth)
        {
            Children = children;
        }

        public IEnumerable<ImmutableWikiEntry> Children { get; private set; }
    }

    public interface IAsyncModel
    {
        /// <summary>
        /// Returns all available model assets
        /// </summary>
        /// <returns></returns>
        Task<IEnumerable<ImmutableWikiEntry>> GetAvailableAssets();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fullPath"></param>
        /// <returns>Task of Tuple, second element will contain the directory only if first element is true</returns>
        Task<Tuple<bool, ImmutableWikiDirectory>> GetDirectory(string fullPath);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fullPath"></param>
        /// <returns>Task of Tuple, second element will contain the page only if first element is true</returns>
        Task<Tuple<bool, ImmutableWikiPage>> GetPageByWikiUrl(string fullPath);
    }

    public interface IAsyncRepository
    {
        IAsyncModel AsyncModel { get; set; }
    }
}
