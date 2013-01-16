using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Icklewik.Core
{
    public enum FilterMode
    {
        PreConversion,
        PostConversion
    }

    public interface IContentFilter
    {
        FilterMode Mode { get; }

        string Apply(string content);
    }
}
