using System.Windows;
using System.Windows.Controls;
using MagazzinoLegname.Services;

namespace MagazzinoLegname.Views;

public partial class ManualPackageRemovalWindow : Window
{
    public ManualPackageRemovalWindow(string packageCode, bool isSupplementary = false)
    {
        InitializeComponent();
        OperatorCombo.ItemsSource = OperatorCatalogService.Shared.ActiveOperatorNames;
        if (isSupplementary)
        {
            var returnItem = ReasonCombo.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(item => Equals(item.Content, "Reso"));
            if (returnItem is not null) ReasonCombo.Items.Remove(returnItem);
        }
        PackageText.Text = $"Pacco {packageCode} · il record resterà consultabile nello Storico.";
    }

    public string OperatorName => OperatorCombo.SelectedItem?.ToString() ?? string.Empty;
    public string Reason => (ReasonCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;
    public string Note => NoteTextBox.Text.Trim();

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (Reason == "Altro" && string.IsNullOrWhiteSpace(Note))
        {
            ValidationText.Text = "Inserire una breve nota per il motivo Altro.";
            NoteTextBox.Focus();
            return;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
