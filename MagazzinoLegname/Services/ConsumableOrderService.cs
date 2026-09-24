using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence;
using MagazzinoLegname.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace MagazzinoLegname.Services;

public sealed class ConsumableOrderConflictException(string message, Exception inner) : InvalidOperationException(message, inner);
public sealed class ConsumableOrderService(IConsumableOrderRepository repository)
{
    public void Save(ConsumableSqlOrder order)
    {
        try { repository.Save(order); }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConsumableOrderConflictException(exception.Message + " Nessuna sovrascrittura eseguita.", exception);
        }
        catch (Exception exception) { throw SqlPersistenceRoot.OperatorException(exception); }
    }
}
