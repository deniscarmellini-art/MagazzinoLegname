using MagazzinoLegname.Infrastructure;
using MagazzinoLegname.Services;
using MagazzinoLegname.Persistence;

namespace MagazzinoLegname.ViewModels;

public abstract class ConsultationViewModel : ObservableObject
{
    private readonly Action _reloadSources;
    private bool _refreshing;
    private bool _isDataAvailable;
    private string _refreshMessage = "Aggiornare i dati da SQL.";
    protected ConsultationViewModel(Action? reloadSources = null)
    {
        _reloadSources = reloadSources ?? ConsultationSqlRefresh.Reload;
        ConsultationSqlRefresh.Starting += Invalidate;
    }
    public bool IsDataAvailable { get => _isDataAvailable; private set => SetProperty(ref _isDataAvailable, value); }
    public string RefreshMessage { get => _refreshMessage; private set => SetProperty(ref _refreshMessage, value); }
    protected bool CanRebuild => IsDataAvailable && !ConsultationSqlRefresh.IsRefreshing;
    private void Invalidate()
    {
        IsDataAvailable = false;
        RefreshMessage = "Dati da aggiornare. Premere Aggiorna da SQL.";
    }
    protected void RefreshFromSql(Action rebuild, Action? reloadAdditionalSources = null)
    {
        if (_refreshing || ConsultationSqlRefresh.IsRefreshing) return;
        _refreshing = true;
        Invalidate();
        try
        {
            ConsultationSqlRefresh.Run(() => { _reloadSources(); reloadAdditionalSources?.Invoke(); });
            IsDataAvailable = true;
            rebuild();
            RefreshMessage = $"Dati aggiornati da SQL alle {DateTime.Now:HH:mm:ss}.";
        }
        catch (Exception error)
        {
            IsDataAvailable = false;
            System.Diagnostics.Debug.WriteLine($"[Consultazione SQL] {error}");
            RefreshMessage = "Impossibile aggiornare la pagina. " + DatabaseErrorTranslator.Translate(error).OperatorMessage
                + " I dati precedenti sono nascosti. Premere Aggiorna da SQL per riprovare.";
        }
        finally { _refreshing = false; }
    }
}
