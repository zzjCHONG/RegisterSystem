using System.Globalization;
using System.Windows;

namespace RegisterSystem;

public partial class CustomerWindow : Window
{
    private const string DateFormat = "yyyy/MM/dd";

    public CustomerWindow()
    {
        InitializeComponent();
        RefreshView();
    }

    private void ActivateButton_Click(object sender, RoutedEventArgs e)
    {
        if (!RegisterService.TryActivateFromRaw(EnrollCodeTextBox.Text, out string error))
        {
            MessageBox.Show(error, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        MessageBox.Show("授权成功。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        EnrollCodeTextBox.Clear();
        RefreshView();
    }

    private void CopyMachineCodeButton_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(MachineCodeTextBox.Text);
        MessageBox.Show("机器码已复制，可发送给供应商生成注册码。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void RefreshView()
    {
        RegisterService.ReadEnrollFile();
        CurrentDateTextBox.Text = DateTime.Now.ToString("yyyy年 M月d日");
        MachineCodeTextBox.Text = RegisterService.GetMachineCode();
        StatusTextBox.Text = RegisterService.RegisterStatus.ToString();
        RemainingTextBox.Text = BuildRemainingText();
    }

    private static string BuildRemainingText()
    {
        return RegisterService.RegisterStatus switch
        {
            RegisterStatus.未注册 => "未注册",
            RegisterStatus.超期 => "已到期",
            RegisterStatus.永久 => "永久授权",
            RegisterStatus.试用 => BuildTrialRemainingText(),
            _ => "未知",
        };
    }

    private static string BuildTrialRemainingText()
    {
        if (!DateTime.TryParseExact(RegisterService.Deadline, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime deadline))
        {
            return "试用期截止日期无效";
        }

        int remainingDays = (deadline.Date - DateTime.Today).Days;
        return remainingDays >= 0 ? $"剩余 {remainingDays} 天（截止 {deadline:yyyy年 M月d日}）" : "已到期";
    }
}
