using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed class ConsumableInventoryPrintService
{
    private const double PageMargin = 34;
    private const double RowHeight = 42;
    private static readonly Brush Ink = Brushes.Black;
    private static readonly Brush Rule = Brushes.Black;

    public void Print(Window? owner, IEnumerable<ConsumableItem> source, DateTime readingDate, string operatorName)
    {
        var items = source.Where(item => item.IsActive)
            .OrderBy(item => item.Department, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.ProductName, StringComparer.CurrentCultureIgnoreCase).ToArray();
        if (items.Length == 0) throw new InvalidOperationException("Non ci sono materiali attivi da stampare.");

        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true) return;
        var width = dialog.PrintableAreaWidth > 0 ? dialog.PrintableAreaWidth : 793.7;
        var height = dialog.PrintableAreaHeight > 0 ? dialog.PrintableAreaHeight : 1122.5;
        var rowsPerPage = Math.Max(8, (int)Math.Floor((height - PageMargin * 2 - 150) / RowHeight));
        var pageCount = (int)Math.Ceiling((double)items.Length / rowsPerPage);
        var document = new FixedDocument { DocumentPaginator = { PageSize = new Size(width, height) } };
        var printedAt = DateTime.Now;

        for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            var pageItems = items.Skip(pageIndex * rowsPerPage).Take(rowsPerPage).ToArray();
            var page = BuildPage(pageItems, readingDate, operatorName, printedAt, pageIndex + 1, pageCount, width, height,
                pageIndex == 0 ? null : items[pageIndex * rowsPerPage - 1].Department);
            var content = new PageContent();
            ((IAddChild)content).AddChild(page);
            document.Pages.Add(content);
        }
        dialog.PrintDocument(document.DocumentPaginator, "Inventario materiali di consumo");
    }

    private static FixedPage BuildPage(IReadOnlyList<ConsumableItem> items, DateTime readingDate,
        string operatorName, DateTime printedAt, int pageNumber, int pageCount, double width, double height,
        string? previousDepartment)
    {
        var page = new FixedPage { Width = width, Height = height, Background = Brushes.White };
        var root = new Grid { Width = width - PageMargin * 2, Height = height - PageMargin * 2 };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        FixedPage.SetLeft(root, PageMargin); FixedPage.SetTop(root, PageMargin); page.Children.Add(root);

        root.Children.Add(new TextBlock { Text = "INVENTARIO MATERIALI DI CONSUMO", Foreground = Ink, FontFamily = new FontFamily("Arial"), FontSize = 19, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center });
        var info = new Grid { Margin = new Thickness(0, 14, 0, 12) };
        info.ColumnDefinitions.Add(new ColumnDefinition()); info.ColumnDefinitions.Add(new ColumnDefinition()); info.ColumnDefinitions.Add(new ColumnDefinition());
        info.Children.Add(Info("DATA RILEVAZIONE", readingDate.ToString("dd/MM/yyyy"), 0));
        info.Children.Add(Info("OPERATORE", string.IsNullOrWhiteSpace(operatorName) ? "________________" : operatorName, 1));
        info.Children.Add(Info("DATA/ORA STAMPA", printedAt.ToString("dd/MM/yyyy HH:mm"), 2));
        Grid.SetRow(info, 1); root.Children.Add(info);

        var table = new Grid();
        foreach (var widthValue in new[] { 2.0, 1.35, .5, .8, 1.25, 1.6 }) table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(widthValue, GridUnitType.Star) });
        table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
        AddRow(table, 0, ["PRODOTTO", "REPARTO / UTILIZZO", "UDM", "QTÀ/UDM", "N° UDM RILEVATE", "NOTE"], true, false);
        var department = previousDepartment;
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(RowHeight) });
            var changedDepartment = !string.Equals(department, item.Department, StringComparison.CurrentCultureIgnoreCase);
            AddRow(table, index + 1, [item.ProductName, item.Department, item.UnitOfMeasure, item.QuantityPerUnit?.ToString("N2") ?? "—", string.Empty, string.Empty], false, changedDepartment);
            department = item.Department;
        }
        Grid.SetRow(table, 2); root.Children.Add(table);

        var footer = new TextBlock { Text = $"Pagina {pageNumber} di {pageCount}", Foreground = Ink, FontFamily = new FontFamily("Arial"), FontSize = 9, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        Grid.SetRow(footer, 3); root.Children.Add(footer);
        return page;
    }

    private static Border Info(string label, string value, int column)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = label, Foreground = Ink, FontFamily = new FontFamily("Arial"), FontSize = 8, FontWeight = FontWeights.Bold });
        panel.Children.Add(new TextBlock { Text = value, Foreground = Ink, FontFamily = new FontFamily("Arial"), FontSize = 11, Margin = new Thickness(0, 3, 0, 0) });
        var border = new Border { Child = panel, Padding = new Thickness(8), BorderBrush = Rule, BorderThickness = new Thickness(1), Margin = new Thickness(column == 0 ? 0 : 4, 0, column == 2 ? 0 : 4, 0) };
        Grid.SetColumn(border, column); return border;
    }

    private static void AddRow(Grid table, int row, IReadOnlyList<string> values, bool header, bool departmentChanged)
    {
        for (var column = 0; column < values.Count; column++)
        {
            var text = new TextBlock { Text = values[column], Foreground = Ink, FontFamily = new FontFamily("Arial"), FontSize = header ? 8.5 : 10.5, FontWeight = header || departmentChanged && column == 1 ? FontWeights.Bold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            var border = new Border { Child = text, Padding = new Thickness(6, 3, 6, 3), BorderBrush = Rule, BorderThickness = new Thickness(column == 0 ? 1 : 0, departmentChanged && !header ? 2 : row == 0 ? 1 : 0, 1, 1), Background = header ? Brushes.LightGray : departmentChanged ? new SolidColorBrush(Color.FromRgb(242, 242, 242)) : Brushes.White };
            Grid.SetRow(border, row); Grid.SetColumn(border, column); table.Children.Add(border);
        }
    }
}
