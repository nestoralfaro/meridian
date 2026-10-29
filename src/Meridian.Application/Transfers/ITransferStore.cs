using Meridian.Application.Auditing;
using Meridian.Domain;

namespace Meridian.Application.Transfers;

/// <summary>Persistence for transfers. <see cref="SaveAsync"/> is atomic: both balances, both activities, the transfer and its audit event, or nothing.</summary>
public interface ITransferStore
{
    Task<Transfer?> FindByIdempotencyKeyAsync(Guid clientId, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>Loads the account for change (tracked), without holdings.</summary>
    Task<Account?> LoadAccountAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <exception cref="TransferConcurrencyException">An account changed after it was loaded.</exception>
    /// <exception cref="DuplicateIdempotencyKeyException">Another request with the same key was saved first.</exception>
    Task SaveAsync(Transfer transfer, Transaction outgoing, Transaction incoming, AuditRecord audit, CancellationToken cancellationToken = default);
}

public sealed class TransferConcurrencyException(Exception? innerException = null)
    : Exception("An account changed while the transfer was being made.", innerException);

public sealed class DuplicateIdempotencyKeyException(Exception? innerException = null)
    : Exception("A transfer with this idempotency key already exists.", innerException);
