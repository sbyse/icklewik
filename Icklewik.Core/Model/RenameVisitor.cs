using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Icklewik.Core.Model
{
    public class RenameVisitor : IWikiEntryVisitor
    {
        private string oldPath;
        private string renamedPath;

        private Action<string, string, WikiPage> pageRenameAction;
        private Action<string, string, WikiDirectory> directoryRenameAction;

        public RenameVisitor(string oldPath, string renamedPath, Action<string, string, WikiPage> pageRename, Action<string, string, WikiDirectory> directoryRename = null)
        {
            this.oldPath = oldPath;
            this.renamedPath = renamedPath;

            this.pageRenameAction = pageRename;
            this.directoryRenameAction = directoryRename;
        }

        public void Visit(WikiDirectory directory)
        {
            string oldSourcePath = directory.SourcePath;
            string oldWikiPath = directory.WikiPath;

            CommonVisit(directory);

            foreach (var child in directory.Children)
            {
                child.Accept(this);
            }

            if (directoryRenameAction != null)
            {
                directoryRenameAction(oldSourcePath, oldWikiPath, directory);
            }
        }

        public void Visit(WikiPage page)
        {
            string oldSourcePath = page.SourcePath;
            string oldWikiPath = page.WikiPath;

            CommonVisit(page);

            if (pageRenameAction != null)
            {
                pageRenameAction(oldSourcePath, oldWikiPath, page);
            }
        }

        private void CommonVisit(WikiEntry entry)
        {
            entry.SourcePath.Replace(oldPath, renamedPath);
            entry.WikiPath.Replace(oldPath, renamedPath);
            entry.WikiUrl.Replace(oldPath, renamedPath);
        }
    }
}
