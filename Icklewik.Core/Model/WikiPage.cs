namespace Icklewik.Core.Model
{
    public class WikiPage : WikiEntry
    {
        public override void Accept(IWikiEntryVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
