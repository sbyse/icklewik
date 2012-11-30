namespace Icklewik.Core
{
    /// <summary>
    /// Converts the source file to a valid html file using the supplied dialogue
    /// 
    /// Convertor is expected to be thread-safe
    /// </summary>
    public class Convertor
    {
        private IDialogue dialogueImplementation;

        public Convertor(IDialogue impl, string fileExtension = ".md")
        {
            dialogueImplementation = impl;
            FileExtension = fileExtension;
        }

        public string FileExtension { get; private set; }

        public string FileSearchString
        {
            get
            {
                return "*" + FileExtension;
            }
        }

        public string Convert(string sourceText)
        {
            return dialogueImplementation.Convert(sourceText);
        }
    }
}
