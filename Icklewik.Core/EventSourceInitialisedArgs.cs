using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Icklewik.Core
{
    public class EventSourceInitialisedArgs : WikiEventArgs
    {
        public override string Id
        {
            get 
            {
                return "n/a";
            }
        }
    }
}
