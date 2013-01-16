using Xunit;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Icklewik.Filters.Test
{
    public class TestMarkdownLinkPostFilter
    {
        [Fact]
        public void MarkdownLinksAreConvertedToHtmlLinks()
        {
            string htmlTestPage = @"
                <html>
                    <head></head>
                    <body>
                        <a title=""myLink"" href=""somepath/to/a/file/with/extension.md"">My Link</a>
                    </body>
                </html>";

            MarkdownLinkPostFilter filter = new MarkdownLinkPostFilter();

            string revisedHtmlPage = filter.Apply(htmlTestPage);

            Assert.True(htmlTestPage.Contains("extension.md"));

            Assert.False(revisedHtmlPage.Contains("extension.md"));
            Assert.True(revisedHtmlPage.Contains("extension.html"));
        }
    }
}
