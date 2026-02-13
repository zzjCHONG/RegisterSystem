using System.Windows;

namespace RegisterSystem;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        CurrentDatePicker.SelectedDate = DateTime.Today;
        DeadlineDatePicker.SelectedDate = DateTime.Today.AddDays(30);
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
        DateTime deadline = DeadlineDatePicker.SelectedDate ?? DateTime.Today;
        EnrollPayload payload = RegisterService.BuildPayload(deadline);
        GeneratedCodeTextBox.Text = payload.ToCompactString();
        EnrollCodeTextBox.Text = payload.ToCompactString();
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
