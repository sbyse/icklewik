using System.ComponentModel;

namespace Icklewik.WindowsService
{
    [RunInstaller(true)]
    public sealed class ServiceInstaller : System.ServiceProcess.ServiceInstaller
    {
        public ServiceInstaller()
        {
            this.Description = "Icklewik file-based wiki";
            this.DisplayName = "Icklewik";
            this.ServiceName = "Icklewik";
            this.StartType = System.ServiceProcess.ServiceStartMode.Automatic;
        }
    }
}
