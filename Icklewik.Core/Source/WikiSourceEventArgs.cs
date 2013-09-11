using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Icklewik.Core.Source
{
    public class WikiSourceEventArgs : WikiEventArgs
    {
        private static int DebugCounter = 2000;

        private int debugCount;

        public WikiSourceEventArgs(
            string sourcePath,
            string oldSourcePath)
        {
            SourcePath = sourcePath;
            OldSourcePath = oldSourcePath;

            debugCount = DebugCounter++;
        }

        // relative path to source file
        public string SourcePath { get; private set; }

        // used for "move" events only
        public string OldSourcePath { get; private set; }

        public override string Id
        {
            get 
            {
                return string.Format("#{0} {1}", debugCount, SourcePath);
            }
        }
    }
}
