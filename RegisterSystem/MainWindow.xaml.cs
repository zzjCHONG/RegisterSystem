using System.Windows;

namespace RegisterSystem;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OpenCustomerButton_Click(object sender, RoutedEventArgs e)
    {
        new CustomerWindow().Show();
    }

    private void OpenVendorButton_Click(object sender, RoutedEventArgs e)
    {
        new VendorWindow().Show();
    }
}
