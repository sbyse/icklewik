using System;
using System.ServiceProcess;
using System.Threading;

namespace Icklewik.WindowsService
{
    partial class Service : ServiceBase
    {
        private readonly object serviceLock;
        private bool shuttingDown;

        private Action<string[]> startup;
        private Action shutdown;

        public Service(Action<string[]> startupAction, Action shutdownAction)
        {
            serviceLock = new object();
            shuttingDown = false;

            startup = startupAction;
            shutdown = shutdownAction;

            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            new Thread(() =>
            {
                // call the startup code
                startup(args);
                
                lock (serviceLock)
                {
                    // now we wait on the monitor
                    while (!shuttingDown)
                    {
                        Monitor.Wait(serviceLock);
                    }
                }

                // we are shutting down so make sure we call the shutdown action
                shutdown();
            }).Start();
        }

        protected override void OnStop()
        {
            lock (serviceLock)
            {
                shuttingDown = true;
                Monitor.PulseAll(serviceLock);
            }
        }
    }
}
