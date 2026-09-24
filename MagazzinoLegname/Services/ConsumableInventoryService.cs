using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence;
using MagazzinoLegname.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace MagazzinoLegname.Services;

public sealed class ConsumableInventoryConflictException(string message, Exception inner) : InvalidOperationException(message, inner);

public sealed class ConsumableInventoryService(IConsumableInventoryRepository repository)
{
    public ConsumableInventorySnapshot Load()
    {
        try { return repository.Load(); }
        catch (Exception exception) { throw SqlPersistenceRoot.OperatorException(exception); }
    }

    public void Save(SaveConsumableInventory request)
    {
        try { repository.Save(request); }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConsumableInventoryConflictException(exception.Message + " Nessuna sovrascrittura eseguita.", exception);
        }
        catch (Exception exception) { throw SqlPersistenceRoot.OperatorException(exception); }
    }
}
