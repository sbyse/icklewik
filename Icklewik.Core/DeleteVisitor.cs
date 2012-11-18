using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Icklewik.Core
{
    public class DeleteVisitor : IWikiEntryVisitor
    {
        private Action<WikiPage> pageDeleteAction;
        private Action<WikiDirectory> directoryDeleteAction;

        public DeleteVisitor(Action<WikiPage> pageDelete, Action<WikiDirectory> directoryDelete = null)
        {
            this.pageDeleteAction = pageDelete;
            this.directoryDeleteAction = directoryDelete;
        }

        public void Visit(WikiDirectory directory)
        {
            foreach (var child in directory.Children)
            {
                child.Accept(this);
            }

            if (directoryDeleteAction != null)
            {
                directoryDeleteAction(directory);
            }
        }

        public void Visit(WikiPage page)
        {
            if (pageDeleteAction != null)
            {
                pageDeleteAction(page);
            }
        }
    }
}
