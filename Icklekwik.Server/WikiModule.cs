using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Icklekwik.Server.ViewModels.Wiki;
using Icklewik.Core;
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

            Get["/{site}/{page}"] = parameters =>
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
