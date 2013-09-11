using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Icklewik.Core.Source
{
    public class NullSourceWatcher : ISourceWatcher
    {
        public event Action<object, WikiSourceEventArgs> FileAdded;
        public event Action<object, WikiSourceEventArgs> FileUpdated;
        public event Action<object, WikiSourceEventArgs> FileDeleted;
        public event Action<object, WikiSourceEventArgs> FileMoved;

        public event Action<object, WikiSourceEventArgs> DirectoryAdded;
        public event Action<object, WikiSourceEventArgs> DirectoryUpdated;
        public event Action<object, WikiSourceEventArgs> DirectoryDeleted;
        public event Action<object, WikiSourceEventArgs> DirectoryMoved;

        public void Init()
        {
            // do nothing
        }

        public void Dispose()
        {
            // do nothing
        }
    }
}
