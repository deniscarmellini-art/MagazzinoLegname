using System.Windows.Controls;
using MagazzinoLegname.ViewModels;

namespace MagazzinoLegname.Views;

public partial class StatisticsView : UserControl
{
    public StatisticsView()
    {
        InitializeComponent();
        DataContext = new StatisticsViewModel();
    }
}
