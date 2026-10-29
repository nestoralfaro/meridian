using System.Diagnostics;
using Meridian.Application.Auditing;
using Meridian.Application.Transfers;
using Meridian.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Meridian.Infrastructure.Persistence;

internal sealed class TransferStore(MeridianDbContext db, TimeProvider clock) : ITransferStore
{
    private const string IdempotencyIndex = "IX_Transfers_ClientId_IdempotencyKey";

    public Task<Transfer?> FindByIdempotencyKeyAsync(Guid clientId, string idempotencyKey, CancellationToken cancellationToken = default) =>
        db.Transfers.AsNoTracking().SingleOrDefaultAsync(t => t.ClientId == clientId && t.IdempotencyKey == idempotencyKey, cancellationToken);

    public Task<Account?> LoadAccountAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        db.Accounts.SingleOrDefaultAsync(a => a.Id == accountId, cancellationToken);

    /// <summary>One SaveChanges, so one database transaction: balances (version-checked), activity, transfer and audit event.</summary>
    public async Task SaveAsync(Transfer transfer, Transaction outgoing, Transaction incoming, AuditRecord audit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audit);
        db.Transfers.Add(transfer);
        db.Transactions.AddRange(outgoing, incoming);
        db.AuditEvents.Add(new AuditEvent
        {
            OccurredAt = clock.GetUtcNow(),
            Actor = audit.Actor,
            Action = audit.Action,
            Subject = audit.Subject,
            CorrelationId = Activity.Current?.TraceId.ToHexString(),
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new TransferConcurrencyException(ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: IdempotencyIndex })
        {
            throw new DuplicateIdempotencyKeyException(ex);
        }
        finally
        {
            db.ChangeTracker.Clear(); // a failed attempt must not leave modified accounts behind in this scope
        }
    }
}
