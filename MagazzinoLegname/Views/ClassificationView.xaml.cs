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

    private void StartOfficialLabels_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: MaterialGroupClassification group }) return;
        var packages = ViewModel.GetOfficialPackages(group);
        if (packages.Count == 0) return;
        if (ShowLabelPreview(packages, group) == true)
            ViewModel.MarkOfficialLabelsPrinted(group);
    }

    private void ReprintOfficialLabels_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: MaterialGroupClassification group }) return;
        if (MessageBox.Show("Ristampare le etichette ufficiali già esistenti? Non verranno creati nuovi pacchi.",
                "Ristampa etichette", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var packages = ViewModel.GetOfficialPackages(group);
        if (packages.Count > 0) ShowLabelPreview(packages, group);
    }

    private void AddSupplementaryLabel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: MaterialGroupClassification group }) return;
        try
        {
            var package = ViewModel.CreateSupplementaryPackage(group);
            ShowLabelPreview([package], group);
        }
        catch (InvalidOperationException exception)
        {
            MessageBox.Show(exception.Message, "Etichetta supplementare", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ReprintSupplementaryLabels_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: MaterialGroupClassification group }) return;
        var packages = ViewModel.GetSupplementaryPackages(group);
        if (packages.Count == 0)
        {
            MessageBox.Show("Non ci sono etichette supplementari da ristampare per questo gruppo.",
                "Ristampa supplementari", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var selected = SelectSupplementaryPackage(packages);
        if (selected is not null) ShowLabelPreview([selected], group);
    }

    private PhysicalPackageDraft? SelectSupplementaryPackage(IReadOnlyList<PhysicalPackageDraft> packages)
    {
        var window = new Window
        {
            Title = "Ristampa supplementare",
            Width = 360,
            Height = 190,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this)
        };
        var comboBox = new ComboBox
        {
            ItemsSource = packages,
            DisplayMemberPath = nameof(PhysicalPackageDraft.PackageCode),
            SelectedIndex = 0,
            Margin = new Thickness(0, 8, 0, 18)
        };
        var confirm = new Button
        {
            Content = "Ristampa",
            Width = 110,
            HorizontalAlignment = HorizontalAlignment.Right,
            Style = TryFindResource("PrimaryButtonStyle") as Style
        };
        confirm.Click += (_, _) => window.DialogResult = true;
        window.Content = new StackPanel
        {
            Margin = new Thickness(18),
            Children =
            {
                new TextBlock { Text = "Seleziona l'etichetta supplementare da ristampare.", TextWrapping = TextWrapping.Wrap },
                comboBox,
                confirm
            }
        };
        return window.ShowDialog() == true ? comboBox.SelectedItem as PhysicalPackageDraft : null;
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

    private bool? ShowLabelPreview(IReadOnlyList<PhysicalPackageDraft> packages, MaterialGroupClassification group)
    {
        var preview = new QrPackagePreviewWindow(packages, ViewModel.GetSupplierName(group),
            ViewModel.GetLoadNumber(group), ViewModel.GetDeliveryNoteNumber(group),
            ViewModel.GetCertification(group)) { Owner = Window.GetWindow(this) };
        return preview.ShowDialog();
    }
}