using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Icklewik.Core
{
    /// <summary>
    /// Interface responsible for converting the source text (written in a specific
    /// dialogue) to valid HTML.
    /// 
    /// Implementations are expected to be thread safe
    /// </summary>
    public interface IDialogue
    {
        string Convert(string sourceText);
    }
}
