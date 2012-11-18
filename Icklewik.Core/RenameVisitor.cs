using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Icklewik.Core
{
    public class RenameVisitor : IWikiEntryVisitor
    {
        private string oldPath;
        private string renamedPath;

        private Action<string, WikiPage> pageRenameAction;
        private Action<string, WikiDirectory> directoryRenameAction;

        public RenameVisitor(string oldPath, string renamedPath, Action<string, WikiPage> pageRename, Action<string, WikiDirectory> directoryRename = null)
        {
            this.oldPath = oldPath;
            this.renamedPath = renamedPath;

            this.pageRenameAction = pageRename;
            this.directoryRenameAction = directoryRename;
        }

        public void Visit(WikiDirectory directory)
        {
            string oldWikiPath = directory.WikiPath;

            CommonVisit(directory);

            foreach (var child in directory.Children)
            {
                child.Accept(this);
            }

            if (directoryRenameAction != null)
            {
                directoryRenameAction(oldWikiPath, directory);
            }
        }

        public void Visit(WikiPage page)
        {
            string oldWikiPath = page.WikiPath;

            CommonVisit(page);

            if (pageRenameAction != null)
            {
                pageRenameAction(oldWikiPath, page);
            }
        }

        private void CommonVisit(WikiEntry entry)
        {
            entry.MarkdownPath.Replace(oldPath, renamedPath);
            entry.WikiPath.Replace(oldPath, renamedPath);
            entry.WikiUrl.Replace(oldPath, renamedPath);
        }
    }
}
