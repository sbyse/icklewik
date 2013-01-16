using System.Collections.Generic;

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

            PreConvertors = new List<IContentFilter>();
            PostConvertors = new List<IContentFilter>();
        }

        public string FileExtension { get; private set; }

        public string FileSearchString
        {
            get
            {
                return "*" + FileExtension;
            }
        }

        public IList<IContentFilter> PreConvertors { get; private set; }

        public IList<IContentFilter> PostConvertors { get; private set; }

        public string Convert(string sourceText)
        {
            // update source text from preconvertors
            foreach (var preconvert in PreConvertors)
            {
                sourceText = preconvert.Apply(sourceText);
            }

            string outputText = dialogueImplementation.Convert(sourceText);

            // update output text using postconvertors
            foreach (var postconvert in PostConvertors)
            {
                outputText = postconvert.Apply(outputText);
            }

            return outputText;
        }
    }
}
