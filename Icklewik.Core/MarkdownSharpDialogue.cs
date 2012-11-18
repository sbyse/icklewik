namespace Icklewik.Core
{
    public class MarkdownSharpDialogue : IDialogue
    {
        public string Convert(string markdownText)
        {
            var markdownSharp = new MarkdownSharp.Markdown();

            return markdownSharp.Transform(markdownText);
        }
    }
}
