using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MagazzinoLegname.ViewModels;

namespace MagazzinoLegname.Views;

public partial class MaterialDispatchView : UserControl
{
    private MaterialDispatchViewModel ViewModel => (MaterialDispatchViewModel)DataContext;
    public MaterialDispatchView()
    {
        InitializeComponent();
        DataContext = new MaterialDispatchViewModel();
    }

    private void View_Loaded(object sender, RoutedEventArgs e) => FocusScanner();
    private void QrInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        var result = ViewModel.Scan();
        if (result.CanDischarge) return;
        MessageBox.Show(result.Message, "Scarico materiale", MessageBoxButton.OK, MessageBoxImage.Warning);
        FocusScanner();
    }
    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ConfirmDischarge(); FocusScanner();
    }
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Cancel(); FocusScanner();
    }
    private void FocusScanner()
    {
        QrInputBox.Focus(); Keyboard.Focus(QrInputBox); QrInputBox.SelectAll();
    }
}
