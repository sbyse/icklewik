using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Icklekwik.Server.ViewModels.Wiki
{
    public class SiteModel
    {
        public bool IsPartialView { get; set; }
        public string WikiUrl { get; set; }

        public IEnumerable<Tuple<string, string>> SiteMap { get; set; }
    }
}
