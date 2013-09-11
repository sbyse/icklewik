using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Icklewik.Core.Source
{
    public interface ISourceWatcher : IWikiSourceEvents, IDisposable
    {
        void Init();
    }
}
