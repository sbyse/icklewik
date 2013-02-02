using System.Collections.Generic;

namespace Icklewik.Core.Model
{
    public class WikiDirectory : WikiEntry
    {
        public bool IsRoot
        {
            get
            {
                return Parent == null;
            }
        }

        public IList<WikiEntry> Children { get; set; }

        public override void Accept(IWikiEntryVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
