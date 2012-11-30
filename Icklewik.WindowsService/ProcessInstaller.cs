using System.ComponentModel;
using System.ServiceProcess;

namespace Icklewik.WindowsService
{
    [RunInstaller(true)]
    public sealed class ProcessInstaller : ServiceProcessInstaller
    {
        public ProcessInstaller()
        {
            this.Account = ServiceAccount.NetworkService;
        }
    }
}
