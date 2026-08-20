using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace MagazzinoLegname.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        DataContext = new MagazzinoLegname.ViewModels.DashboardViewModel();
    }

    private void OpenHistory_Click(object sender, RoutedEventArgs e) =>
        (Window.GetWindow(this) as MainWindow)?.NavigateToHistory();
}

public sealed class MovementSignConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is decimal cubicMeters
            ? cubicMeters > 0m ? "Positive" : cubicMeters < 0m ? "Negative" : "Neutral"
            : "Neutral";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
