namespace Icklewik.Core
{
    public class Convertor
    {
        private IDialogue dialogueImplementation;

        public Convertor(IDialogue impl, string fileExtension = ".md")
        {
            dialogueImplementation = impl;
            FileExtension = fileExtension;
        }

        public string FileExtension { get; set; }

        public string FileSearchString
        {
            get
            {
                return "*" + FileExtension;
            }
        }

        public string Convert(string markdownText)
        {
            return dialogueImplementation.Convert(markdownText);
        }
    }
}
