using System.Windows.Controls;

namespace MagazzinoLegname.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        DataContext = new MagazzinoLegname.ViewModels.DashboardViewModel();
    }
}
