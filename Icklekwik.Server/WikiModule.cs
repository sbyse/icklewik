using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Icklekwik.Core.Cache;
using Icklekwik.Server.ViewModels.Wiki;
using Icklewik.Core;
using Icklewik.Core.File;
using Icklewik.Core.Model;
using Nancy;

namespace Icklekwik.Server
{
    public class WikiModule : NancyModule
    {
        private ServerConfig config;
        private FileReader fileReader;

        public WikiModule(ServerConfig serverConfig)
            : base("/wiki")
        {
            config = serverConfig;
            fileReader = new FileReader(FileReaderPolicy.LimitedBlock, 500);

            // add top level "site" route
            Get["/{site}"] = parameters =>
                {
                    WikiConfig wikiConfig = null;
                    MasterRepository masterRepository = null;
                    if (config.TryGetConfig(parameters["site"], out wikiConfig) &&
                        config.TryGetMasterRepository(parameters["site"], out masterRepository))
                    {
                        // TODO: Async-ify this
                        var pageResults = masterRepository.GetAvailableAssets().Result;

                        SiteModel model = new SiteModel()
                        {
                            IsPartialView = Request.Query.isPartial,
                            WikiUrl = "/",
                            SiteMap = pageResults
                        };

                        Context.ViewBag.SiteName = wikiConfig.SiteName;

                        return View["Site.cshtml", model];
                    }
                    else
                    {
                        return HttpStatusCode.NotFound;
                    }
                };

            // add "directory" route, subpath should not contain a "." or a "/"
            Get[@"/{site}/(?<directory>[^\.]*)"] = parameters =>
                {
                    WikiConfig wikiConfig;
                    if (config.TryGetConfig(parameters["site"], out wikiConfig) && 
                        Directory.Exists(Path.Combine(wikiConfig.RootWikiPath, parameters["directory"])))
                    {
                        DirectoryModel model = new DirectoryModel()
                        {
                            IsPartialView = Request.Query.isPartial,
                            WikiUrl = parameters["directory"],
                        };

                        Context.ViewBag.SiteName = wikiConfig.SiteName;

                        return View["Directory.cshtml", model];
                    }
                    else
                    {
                        return HttpStatusCode.NotFound;
                    }
                };

            // add "page" route, subpath should always have a file extension (and therefore at least one ".")
            Get[@"/{site}/(?<page>.*\..*)"] = parameters => 
                {
                    WikiConfig wikiConfig = null;
                    MasterRepository masterRepository = null;
                    if (config.TryGetConfig(parameters["site"], out wikiConfig) &&
                        config.TryGetMasterRepository(parameters["site"], out masterRepository))
                    {
                        // Async-ify
                        var results = masterRepository.GetPageByWikiUrl(parameters["page"]).Result;

                        if (results.Item1)
                        {
                            var results2 = TryGetPageContents(parameters["site"], results.Item2);

                            if (results2.Item1)
                            {
                                PageModel model = new PageModel()
                                {
                                    IsPartialView = Request.Query.isPartial,
                                    WikiUrl = parameters["page"],
                                    Contents = results2.Item2
                                };

                                Context.ViewBag.SiteName = wikiConfig.SiteName;

                                return View["Page.cshtml", model];
                            }
                            else
                            {
                                // TODO: If the file doesn't exist we could potentially remove it from the cache
                                return HttpStatusCode.NotFound;
                            }
                        }
                        else
                        {
                            // TODO: If the file doesn't exist we could potentially remove it from the cache
                            return HttpStatusCode.NotFound;
                        }
                    }
                    else
                    {
                        return HttpStatusCode.NotFound;
                    }
                };
        }

        private async Task<Tuple<bool, string>> TryGetPageContents(string siteName, ImmutableWikiPage page)
        {
            IPageCache pageCache = null;
            string content = null;

            // TODO: Async-ify
            bool hasCache = config.TryGetPageCache(siteName, out pageCache);
            bool pageContentsCached = hasCache && pageCache.TryGetContents(page.WikiUrl, out content);

            Tuple<bool, string> results = new Tuple<bool, string>(false, string.Empty);
            if (!pageContentsCached)
            {
                results = await fileReader.TryReadFile(page.WikiPath);
            }
                
            // cache the contents if they weren't already
            if (hasCache && !pageContentsCached && results.Item1)
            {
                pageCache.CachePageContents(page.WikiUrl, results.Item2);
            }
            
            return results;
        }
    }
}
