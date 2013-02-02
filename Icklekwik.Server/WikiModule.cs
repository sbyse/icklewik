using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Icklekwik.Server.ViewModels.Wiki;
using Icklewik.Core;
using Icklewik.Core.Model;
using Nancy;

namespace Icklekwik.Server
{
    public class WikiModule : NancyModule
    {
        private ServerConfig config;

        public WikiModule(ServerConfig serverConfig)
            : base("/wiki")
        {
            config = serverConfig;

            // add top level "site" route
            Get["/{site}"] = parameters =>
                {
                    WikiConfig wikiConfig = null;
                    SlaveRepository slaveRepository = null;
                    if (config.TryGetConfig(parameters["site"], out wikiConfig) &&
                        config.TryGetRepository(parameters["site"], out slaveRepository))
                    {
                        SiteModel model = new SiteModel()
                        {
                            IsPartialView = Request.Query.isPartial,
                            WikiUrl = "/",
                            SiteMap = slaveRepository.Model.AvailableAssets.Select(a => new Tuple<string, string>(a, "Details"))
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
                    WikiConfig wikiConfig;
                    if (config.TryGetConfig(parameters["site"], out wikiConfig) && 
                        File.Exists(Path.Combine(wikiConfig.RootWikiPath, parameters["page"])))
                    {
                        PageModel model = new PageModel()
                        {
                            IsPartialView = Request.Query.isPartial,
                            WikiUrl = parameters["page"],
                            Contents = File.ReadAllText(Path.Combine(wikiConfig.RootWikiPath, parameters["page"]))
                        };

                        Context.ViewBag.SiteName = wikiConfig.SiteName;

                        return View["Page.cshtml", model];
                    }
                    else
                    {
                        return HttpStatusCode.NotFound;
                    }
                };
        }
    }
}
