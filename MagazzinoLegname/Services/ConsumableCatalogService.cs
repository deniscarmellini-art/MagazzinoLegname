using System.Collections.ObjectModel;
using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence;
using MagazzinoLegname.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace MagazzinoLegname.Services;

// SQL-only catalog. Legacy import and operational collections are deliberately separate.
public sealed class ConsumableCatalogRefreshException(string message, Exception? inner = null) : InvalidOperationException(message, inner);

public sealed class ConsumableCatalogService(IConsumableRepository repository)
{
    public static ConsumableCatalogService Shared { get; } = new(SqlPersistenceRoot.Consumables);
    public ObservableCollection<ConsumableItem> Items { get; } = [];

    public void Reload()
    {
        Items.Clear();
        try
        {
            foreach (var item in repository.GetAll()) Items.Add(item);
        }
        catch (Exception exception) { throw SqlPersistenceRoot.OperatorException(exception); }
    }

    public ConsumableItem LoadDetail(Guid id)
    {
        try { return repository.Get(id) ?? throw new InvalidOperationException("L'articolo non esiste più. Ricaricare la lista."); }
        catch (Exception exception) { throw SqlPersistenceRoot.OperatorException(exception); }
    }

    public ConsumableItem CreateDraft() => new()
    {
        // Client-generated unique code avoids racing on an in-memory MAX + 1.
        InternalCode = $"CON-{Guid.NewGuid():N}", IsActive = true
    };

    public void Save(ConsumableItem item)
    {
        try { repository.Save(item); }
        catch (DbUpdateConcurrencyException)
        {
            try { Reload(); }
            catch (Exception reloadError)
            {
                throw new ConsumableCatalogRefreshException("Conflitto: articolo modificato o eliminato da un'altra postazione. Nessuna sovrascrittura. Ricaricamento SQL non riuscito: " + reloadError.Message, reloadError);
            }
            throw new ConsumableCatalogRefreshException("L'articolo è stato modificato o eliminato da un'altra postazione. Nessuna modifica sovrascritta. I dati sono stati ricaricati da SQL: riaprire l'articolo e verificare i valori.");
        }
        catch (Exception exception) { throw SqlPersistenceRoot.OperatorException(exception); }
        try { Reload(); }
        catch (Exception exception)
        {
            throw new ConsumableCatalogRefreshException("Salvataggio SQL eseguito, ma ricaricamento non riuscito. Riaprire la lista prima di altre modifiche. " + exception.Message, exception);
        }
    }
}
