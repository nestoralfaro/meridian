using Meridian.Domain;

namespace Meridian.Application.Transfers;

/// <param name="IdempotencyKey">Chosen by the client per intended transfer; retrying with the same key never moves money twice.</param>
/// <param name="ExpectedFromVersion">The source account's <see cref="Account.Version"/> when the client reviewed the transfer.</param>
public sealed record TransferRequest(
    Guid ClientId, Guid FromAccountId, Guid ToAccountId, Money Amount, string IdempotencyKey, uint ExpectedFromVersion);

public enum TransferStatus
{
    Completed,

    /// <summary>The same request was already completed with this key; nothing moved this time.</summary>
    Replayed,

    /// <summary>The key was already used for a different transfer.</summary>
    KeyReused,

    /// <summary>Missing, or another client's. Hosts must answer identically for both (no enumeration).</summary>
    AccountNotFound,

    SameAccount,
    InvalidAmount,
    InsufficientCash,

    /// <summary>The source account changed since it was reviewed, or during the transfer. Review again.</summary>
    Conflict,
}

public sealed record TransferView(Guid Id, Guid FromAccountId, Guid ToAccountId, Money Amount, DateTimeOffset RequestedAt);

/// <param name="Transfer">Set for <see cref="TransferStatus.Completed"/> and <see cref="TransferStatus.Replayed"/>.</param>
/// <param name="AvailableCash">Set for <see cref="TransferStatus.InsufficientCash"/>.</param>
/// <param name="ForeignAccountId">Set when an account in the request belongs to another client, so hosts can audit it.</param>
public sealed record TransferOutcome(TransferStatus Status, TransferView? Transfer = null, Money? AvailableCash = null, Guid? ForeignAccountId = null);
