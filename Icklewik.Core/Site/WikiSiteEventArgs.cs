using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Icklewik.Core.Site
{
    public class WikiSiteEventArgs : WikiEventArgs
    {
        private static int DebugCounter = 1000;

        private int debugCount;

        public WikiSiteEventArgs(
            string wikiUrl,
            string oldWikiUrl)
        {
            WikiUrl = wikiUrl;
            OldWikiUrl = oldWikiUrl;

            debugCount = DebugCounter++;
        }

        // relative url to wiki page
        public string WikiUrl { get; private set; }

        // previous relative url to wiki page, only relevant for "move" events
        public string OldWikiUrl { get; private set; }

        public override string Id
        {
            get 
            {
                return string.Format("#{0} {1}", debugCount, WikiUrl);
            }
        }
    }
}
