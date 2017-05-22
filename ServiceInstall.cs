using System;
using System.ComponentModel;
using System.ServiceProcess;
using System.Configuration.Install;

namespace WinDHCP
{
    [RunInstaller(true)]
    public partial class ServiceInstall : System.Configuration.Install.Installer
    {
        ServiceInstaller serviceInstaller;
        ServiceProcessInstaller processInstaller;

        public ServiceInstall()
        {
            InitializeComponent();
            serviceInstaller = new ServiceInstaller();
            processInstaller = new ServiceProcessInstaller();

            processInstaller.Account = ServiceAccount.LocalSystem;
            serviceInstaller.StartType = ServiceStartMode.Manual;
            serviceInstaller.ServiceName = "DhcpService";
            Installers.Add(processInstaller);
            Installers.Add(serviceInstaller);
        }
    }
}
