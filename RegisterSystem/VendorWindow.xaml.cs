using System.Windows;
using System.Windows.Controls;

namespace RegisterSystem;

public partial class VendorWindow : Window
{
    private static readonly DateTime PermanentDeadline = new(2122, 12, 31);

    public VendorWindow()
    {
        InitializeComponent();
        CurrentDatePicker.SelectedDate = DateTime.Today;
        LicenseTypeComboBox.SelectedIndex = 0;
        DeadlineDatePicker.SelectedDate = ResolveDeadline(DateTime.Today);
        DeadlineDatePicker.IsEnabled = false;
    }

    private void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        string machineCode = MachineCodeTextBox.Text.Trim();
        if (machineCode.Length != 24)
        {
            MessageBox.Show("请输入客户提供的24位机器码。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DateTime currentDate = CurrentDatePicker.SelectedDate ?? DateTime.Today;
        DateTime deadline = ResolveDeadline(currentDate);
        EnrollPayload payload = RegisterService.BuildPayload(machineCode, deadline, currentDate);
        GeneratedCodeTextBox.Text = payload.ToCompactString();
    }

    private void LicenseTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        DateTime currentDate = CurrentDatePicker.SelectedDate ?? DateTime.Today;
        bool isCustom = (LicenseTypeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() == "自定义日期";
        DeadlineDatePicker.IsEnabled = isCustom;

        if (!isCustom)
        {
            DeadlineDatePicker.SelectedDate = ResolveDeadline(currentDate);
        }
    }

    private void CurrentDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
    {
        if ((LicenseTypeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() != "自定义日期")
        {
            DateTime currentDate = CurrentDatePicker.SelectedDate ?? DateTime.Today;
            DeadlineDatePicker.SelectedDate = ResolveDeadline(currentDate);
        }
    }

    private DateTime ResolveDeadline(DateTime currentDate)
    {
        string selected = (LicenseTypeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "3天";
        return selected switch
        {
            "3天" => currentDate.AddDays(3),
            "10天" => currentDate.AddDays(10),
            "20天" => currentDate.AddDays(20),
            "一个月" => currentDate.AddMonths(1),
            "三个月" => currentDate.AddMonths(3),
            "一年" => currentDate.AddYears(1),
            "永久" => PermanentDeadline,
            "自定义日期" => DeadlineDatePicker.SelectedDate ?? currentDate,
            _ => currentDate.AddDays(3),
        };
    }
}
