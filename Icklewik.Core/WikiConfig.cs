using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Icklewik.Core
{
    public class WikiConfig
    {
        public string SiteName { get; set; } 
        public string RootSourcePath { get; set; }
        public string RootWikiPath { get; set; }
        public Convertor Convertor { get; set; }
    }
}
