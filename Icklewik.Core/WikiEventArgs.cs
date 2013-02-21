using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Icklewik.Core
{
    public abstract class WikiEventArgs : EventArgs
    {
        public abstract string Id { get; }
    }
}
