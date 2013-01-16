using HtmlAgilityPack;
using Icklewik.Core;

namespace Icklewik.Filters
{
    public class MarkdownLinkPostFilter : IContentFilter
    {
        public FilterMode Mode
        {
            get
            {
                return FilterMode.PostConversion;
            }
        }

        public string Apply(string content)
        {
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(content);

            var nodeCollection = doc.DocumentNode.SelectNodes("//a[@href]");

            if (nodeCollection != null)
            {
                foreach (HtmlNode link in nodeCollection)
                {
                    HtmlAttribute att = link.Attributes["href"];

                    if (att.Value.EndsWith(".md"))
                    {
                        att.Value = att.Value.Substring(0, att.Value.Length - 2) + "html";
                    }
                }
            }

            return doc.DocumentNode.OuterHtml;
        }
    }
}
