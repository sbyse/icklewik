using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Icklewik.Core.Logging
{
    public static class Log
    {
        public static ILogger Instance = new ConsoleLogger();
    }
}
