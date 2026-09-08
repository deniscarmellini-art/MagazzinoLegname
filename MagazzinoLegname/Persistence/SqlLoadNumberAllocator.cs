using System.Data;
using MagazzinoLegname.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MagazzinoLegname.Persistence;

public sealed class SqlLoadNumberAllocator(IDbContextFactory<MagazzinoDbContext> contextFactory)
{
    public async Task<int> ReserveNextAsync(Guid supplierId, int loadYear, CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                var sequence = await context.LoadNumberSequences.SingleOrDefaultAsync(
                    item => item.SupplierId == supplierId && item.LoadYear == loadYear, cancellationToken);
                int reserved;
                if (sequence is null)
                {
                    var lastUsed = await context.Loads.Where(item => item.SupplierId == supplierId && item.LoadYear == loadYear)
                        .MaxAsync(item => (int?)item.AnnualProgressive, cancellationToken) ?? 0;
                    reserved = lastUsed + 1;
                    context.LoadNumberSequences.Add(new LoadNumberSequenceEntity
                    {
                        SupplierId = supplierId, LoadYear = loadYear, NextProgressive = reserved + 1
                    });
                }
                else
                {
                    reserved = sequence.NextProgressive;
                    sequence.NextProgressive++;
                }
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return reserved;
            }
            catch (Exception exception) when (attempt < 3 && DatabaseErrorTranslator.Translate(exception).Kind is DatabaseFailureKind.UniqueConstraint or DatabaseFailureKind.ConcurrencyConflict)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
        }
        throw new InvalidOperationException("Non è stato possibile riservare un nuovo progressivo carico dopo più tentativi concorrenti.");
    }
}
