﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Icklewik.Core
{
    public interface IDialogue
    {
        string Convert(string markdownText);
    }
}
