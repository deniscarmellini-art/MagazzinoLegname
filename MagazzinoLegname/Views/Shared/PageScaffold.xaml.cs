using System.Windows;
using System.Windows.Controls;

namespace MagazzinoLegname.Views.Shared;

public partial class PageScaffold : UserControl
{
    public static readonly DependencyProperty PageTitleProperty = DependencyProperty.Register(
        nameof(PageTitle), typeof(string), typeof(PageScaffold), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty PageSubtitleProperty = DependencyProperty.Register(
        nameof(PageSubtitle), typeof(string), typeof(PageScaffold), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty PageContentProperty = DependencyProperty.Register(
        nameof(PageContent), typeof(object), typeof(PageScaffold), new PropertyMetadata(null));

    public PageScaffold() => InitializeComponent();
    public string PageTitle { get => (string)GetValue(PageTitleProperty); set => SetValue(PageTitleProperty, value); }
    public string PageSubtitle { get => (string)GetValue(PageSubtitleProperty); set => SetValue(PageSubtitleProperty, value); }
    public object? PageContent { get => GetValue(PageContentProperty); set => SetValue(PageContentProperty, value); }
}
