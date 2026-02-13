using System.Windows;

namespace RegisterSystem;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        bool vendorMode = e.Args.Any(arg => string.Equals(arg, "--vendor", StringComparison.OrdinalIgnoreCase));

        Window startupWindow = vendorMode ? new VendorWindow() : new CustomerWindow();
        MainWindow = startupWindow;
        startupWindow.Show();
    }
}
