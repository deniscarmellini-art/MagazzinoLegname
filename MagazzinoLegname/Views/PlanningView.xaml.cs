using System.Windows.Controls;
using MagazzinoLegname.ViewModels;

namespace MagazzinoLegname.Views;

public partial class PlanningView : UserControl
{
    public PlanningView()
    {
        InitializeComponent();
        DataContext = new PlanningViewModel();
    }
}
