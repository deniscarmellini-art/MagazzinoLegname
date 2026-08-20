using System.Windows.Controls;
using MagazzinoLegname.Views;

namespace MagazzinoLegname.Navigation;

public sealed class NavigationService
{
    private readonly Dictionary<PageKey, Func<UserControl>> _pageFactories = new()
    {
        [PageKey.Dashboard] = () => new DashboardView(),
        [PageKey.GoodsReceipt] = () => new GoodsReceiptView(),
        [PageKey.Classification] = () => new ClassificationView(),
        [PageKey.WasteCorrection] = () => new WasteCorrectionView(),
        [PageKey.MaterialDispatch] = () => new MaterialDispatchView(),
        [PageKey.Inventory] = () => new InventoryView(),
        [PageKey.Planning] = () => new PlanningView(),
        [PageKey.History] = () => new HistoryView(),
        [PageKey.Statistics] = () => new StatisticsView(),
        [PageKey.Settings] = () => new SettingsView()
    };
    private readonly Dictionary<PageKey, UserControl> _pages = [];

    public event EventHandler<UserControl>? PageChanged;

    public void NavigateTo(PageKey pageKey)
    {
        if (!_pages.TryGetValue(pageKey, out var page))
        {
            page = _pageFactories[pageKey]();
            _pages[pageKey] = page;
        }
        PageChanged?.Invoke(this, page);
    }
}
