using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Icklekwik.Server.ViewModels.Wiki
{
    public class PageModel
    {
        public bool IsPartialView { get; set; }
        public string WikiUrl { get; set; }
        public string Contents { get; set; }
    }
}
