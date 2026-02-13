using System.Windows;

namespace RegisterSystem;

public partial class App : Application
{
    // true: 启动供应商界面；false: 启动客户端主程序
    private const bool StartVendorWindow = false;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 供应商工具不做授权拦截，直接进入供应商界面
        if (StartVendorWindow)
        {
            MainWindow = new VendorWindow();
            MainWindow.Show();
            return;
        }

        // 客户端主程序启动前先做授权校验（可直接复用到你的其他程序）
        if (!EnsureLicenseOrExit())
        {
            Shutdown();
            return;
        }

        // 校验通过后，进入你真正的业务主窗口（示例里仍用客户界面）
        MainWindow = new CustomerWindow();
        MainWindow.Show();
    }

    private static bool EnsureLicenseOrExit()
    {
        RegisterService.ReadEnrollFile();

        if (RegisterService.RegisterStatus is RegisterStatus.永久 or RegisterStatus.试用)
        {
            return true;
        }

        string reason = RegisterService.RegisterStatus switch
        {
            RegisterStatus.超期 => "授权已到期，请联系供应商续期。",
            _ => "程序未授权，请先完成注册。",
        };

        MessageBox.Show(
            $"{reason}\n\n机器码：{RegisterService.GetMachineCode()}",
            "授权校验失败",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        return false;
    }
}
