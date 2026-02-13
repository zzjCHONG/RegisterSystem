using System.Windows;
using System.Windows.Controls;

namespace RegisterSystem;

public partial class MainWindow : Window
{
    private static readonly DateTime PermanentDeadline = new(2122, 12, 31);

    public MainWindow()
    {
        InitializeComponent();
        CurrentDatePicker.SelectedDate = DateTime.Today;
        LicenseTypeComboBox.SelectedIndex = 0;
        DeadlineDatePicker.SelectedDate = ResolveDeadline(DateTime.Today);
        DeadlineDatePicker.IsEnabled = false;
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
        RefreshView();
    }

    private void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        DateTime currentDate = CurrentDatePicker.SelectedDate ?? DateTime.Today;
        DateTime deadline = ResolveDeadline(currentDate);
        EnrollPayload payload = RegisterService.BuildPayload(deadline, currentDate);
        string compact = payload.ToCompactString();

        GeneratedCodeTextBox.Text = compact;
        EnrollCodeTextBox.Text = compact;
    }

    private void LicenseTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        DateTime currentDate = CurrentDatePicker.SelectedDate ?? DateTime.Today;
        if (LicenseTypeComboBox.SelectedItem is not ComboBoxItem selected)
        {
            return;
        }

        bool isCustom = selected.Content?.ToString() == "自定义日期";
        DeadlineDatePicker.IsEnabled = isCustom;
        if (!isCustom)
        {
            DeadlineDatePicker.SelectedDate = ResolveDeadline(currentDate);
        }
    }


    private void CurrentDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
    {
        DateTime currentDate = CurrentDatePicker.SelectedDate ?? DateTime.Today;
        if ((LicenseTypeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() != "自定义日期")
        {
            DeadlineDatePicker.SelectedDate = ResolveDeadline(currentDate);
        }
    }

    private DateTime ResolveDeadline(DateTime currentDate)
    {
        string selected = (LicenseTypeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "一个月";

        return selected switch
        {
            "一个月" => currentDate.AddMonths(1),
            "三个月" => currentDate.AddMonths(3),
            "一年" => currentDate.AddYears(1),
            "永久" => PermanentDeadline,
            "自定义日期" => DeadlineDatePicker.SelectedDate ?? currentDate,
            _ => currentDate.AddMonths(1),
        };
    }

    private void RefreshView()
    {
        RegisterService.ReadEnrollFile();
        CurrentDateTextBox.Text = DateTime.Now.ToString("yyyy年 M月d日");
        MachineCodeTextBox.Text = RegisterService.GetMachineCode();
        StatusTextBox.Text = RegisterService.RegisterStatus.ToString();
        RegisterFilePathTextBox.Text = RegisterService.RegisterFilePath;
    }
}
