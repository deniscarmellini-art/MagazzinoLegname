using System.Windows;
using System.Windows.Controls;
using MagazzinoLegname.Models;
using MagazzinoLegname.ViewModels;

namespace MagazzinoLegname.Views;

public partial class ClassificationView : UserControl
{
    private ClassificationViewModel ViewModel => (ClassificationViewModel)DataContext;
    public ClassificationView()
    {
        InitializeComponent();
        DataContext = new ClassificationViewModel();
    }

    private void MarkClassified_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: MaterialGroupClassification group })
            ViewModel.MarkGroupAsClassified(group);
    }

    private void UndoClassification_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: MaterialGroupClassification group })
            ViewModel.UndoGroupClassification(group);
    }
}
