using System.Windows;

namespace MagazzinoLegname.Views;

public partial class PrintConfirmationWindow : Window
{
    public PrintConfirmationWindow() => InitializeComponent();
    public bool PrintSucceeded { get; private set; }
    private void PrintOk_Click(object sender, RoutedEventArgs e) { PrintSucceeded = true; DialogResult = true; }
    private void Reprint_Click(object sender, RoutedEventArgs e) { PrintSucceeded = false; DialogResult = false; }
}
