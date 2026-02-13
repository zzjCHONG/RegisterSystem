using System.Windows;

namespace RegisterSystem;

public partial class App : Application
{
    // true: 启动供应商界面；false: 启动客户界面
    private const bool StartVendorWindow = true;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Window startupWindow = StartVendorWindow ? new VendorWindow() : new CustomerWindow();
        MainWindow = startupWindow;
        startupWindow.Show();
    }
}
