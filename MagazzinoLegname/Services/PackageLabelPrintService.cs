using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using MagazzinoLegname.ViewModels;
using MagazzinoLegname.Views;

namespace MagazzinoLegname.Services;

public sealed class PackageLabelPrintService
{
    public bool Print(IReadOnlyList<PackageLabelViewModel> labels, string jobName)
    {
        if (labels.Count == 0) return false;
        var dialog = new PrintDialog();
        dialog.PrintTicket.PageOrientation = PageOrientation.Landscape;
        dialog.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA4);
        if (dialog.ShowDialog() != true) return false;
        var document = CreateDocument(labels);
        dialog.PrintDocument(document.DocumentPaginator, jobName);
        return true;
    }

    private static FixedDocument CreateDocument(IEnumerable<PackageLabelViewModel> labels)
    {
        var document = new FixedDocument();
        document.DocumentPaginator.PageSize = new Size(PackageLabelViewModel.A4LandscapeWidth, PackageLabelViewModel.A4LandscapeHeight);
        foreach (var label in labels)
        {
            var page = new FixedPage { Width = PackageLabelViewModel.A4LandscapeWidth, Height = PackageLabelViewModel.A4LandscapeHeight, Background = Brushes.White };
            var view = new PackageLabelView { DataContext = label };
            FixedPage.SetLeft(view, 0); FixedPage.SetTop(view, 0);
            page.Children.Add(view);
            var content = new PageContent();
            ((IAddChild)content).AddChild(page);
            document.Pages.Add(content);
        }
        return document;
    }
}
