using System.Windows;
using System.Windows.Controls;

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

        string machineCode = RegisterService.GetMachineCode();
        string title = RegisterService.RegisterStatus == RegisterStatus.超期 ? "授权已到期" : "授权校验失败";
        string message = RegisterService.RegisterStatus == RegisterStatus.超期
            ? "授权已到期，请联系供应商续期。"
            : "程序未授权，请先完成注册。";

        ShowLicenseDialogWithCopy(title, message, machineCode);
        return false;
    }

    private static void ShowLicenseDialogWithCopy(string title, string message, string machineCode)
    {
        Window dialog = new()
        {
            Title = title,
            Width = 460,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
        };

        Grid layout = new() { Margin = new Thickness(16) };
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        TextBlock text = new()
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
        };
        Grid.SetRow(text, 0);

        TextBlock machineCodeText = new()
        {
            Text = $"机器码：{machineCode}",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
        };
        Grid.SetRow(machineCodeText, 1);

        StackPanel actions = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetRow(actions, 3);

        Button copyButton = new()
        {
            Content = "复制机器码",
            Width = 100,
            Margin = new Thickness(0, 0, 8, 0),
        };
        copyButton.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(machineCode);
                MessageBox.Show("机器码已复制。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            dialog.Close();
        };

        Button closeButton = new()
        {
            Content = "关闭",
            Width = 80,
        };
        closeButton.Click += (_, _) => dialog.Close();

        actions.Children.Add(copyButton);
        actions.Children.Add(closeButton);

        layout.Children.Add(text);
        layout.Children.Add(machineCodeText);
        layout.Children.Add(actions);
        dialog.Content = layout;
        dialog.ShowDialog();
    }
}
