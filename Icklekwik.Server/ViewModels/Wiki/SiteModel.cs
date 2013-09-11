using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Icklewik.Core.Model;

namespace Icklekwik.Server.ViewModels.Wiki
{
    public class SiteModel
    {
        public bool IsPartialView { get; set; }
        public string WikiUrl { get; set; }

        public IEnumerable<ImmutableWikiEntry> SiteMap { get; set; }
    }
}
