using Meridian.Application.Auditing;
using Meridian.Domain;

namespace Meridian.Application.Transfers;

/// <summary>Moves cash between a client's own accounts: idempotent per key, optimistic about concurrency, audited.</summary>
public sealed class TransferCommands(ITransferStore store, TimeProvider clock)
{
    public const decimal MaxAmount = 1_000_000m;

    public async Task<TransferOutcome> TransferAsync(TransferRequest request, string actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var previous = await store.FindByIdempotencyKeyAsync(request.ClientId, request.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        if (previous is not null)
        {
            return Replay(previous, request);
        }

        if (request.FromAccountId == request.ToAccountId)
        {
            return new TransferOutcome(TransferStatus.SameAccount);
        }

        if (request.Amount.Amount <= 0m || request.Amount.Amount > MaxAmount || request.Amount.Amount != decimal.Round(request.Amount.Amount, 2))
        {
            return new TransferOutcome(TransferStatus.InvalidAmount);
        }

        var from = await store.LoadAccountAsync(request.FromAccountId, cancellationToken).ConfigureAwait(false);
        var to = await store.LoadAccountAsync(request.ToAccountId, cancellationToken).ConfigureAwait(false);
        foreach (var (account, id) in new[] { (from, request.FromAccountId), (to, request.ToAccountId) })
        {
            if (account is null)
            {
                return new TransferOutcome(TransferStatus.AccountNotFound);
            }

            if (account.ClientId != request.ClientId)
            {
                return new TransferOutcome(TransferStatus.AccountNotFound, ForeignAccountId: id);
            }
        }

        if (from!.Version != request.ExpectedFromVersion)
        {
            return new TransferOutcome(TransferStatus.Conflict);
        }

        if (request.Amount.Currency != from.Cash.Currency || request.Amount > from.Cash)
        {
            return new TransferOutcome(TransferStatus.InsufficientCash, AvailableCash: from.Cash);
        }

        var (transfer, outgoing, incoming) = Transfer.Between(from, to!, request.Amount, request.IdempotencyKey, clock.GetUtcNow());
        try
        {
            await store.SaveAsync(transfer, outgoing, incoming, new AuditRecord(actor, "TransferCompleted", $"transfer:{transfer.Id}"), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is TransferConcurrencyException or DuplicateIdempotencyKeyException)
        {
            // If a concurrent request with the same key won, this is a retry of it, whichever check (the account's
            // version or the key's unique index) the database tripped first. Otherwise the account really changed.
            var winner = await store.FindByIdempotencyKeyAsync(request.ClientId, request.IdempotencyKey, cancellationToken).ConfigureAwait(false);
            return winner is null ? new TransferOutcome(TransferStatus.Conflict) : Replay(winner, request);
        }

        return new TransferOutcome(TransferStatus.Completed, ToView(transfer));
    }

    private static TransferOutcome Replay(Transfer previous, TransferRequest request) =>
        previous.FromAccountId == request.FromAccountId && previous.ToAccountId == request.ToAccountId && previous.Amount == request.Amount
            ? new TransferOutcome(TransferStatus.Replayed, ToView(previous))
            : new TransferOutcome(TransferStatus.KeyReused);

    private static TransferView ToView(Transfer t) => new(t.Id, t.FromAccountId, t.ToAccountId, t.Amount, t.RequestedAt);
}
