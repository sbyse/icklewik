using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Icklewik.Core
{
    public interface IWikiEntryVisitor
    {
        void Visit(WikiDirectory directory);
        void Visit(WikiPage page);
    }
}
