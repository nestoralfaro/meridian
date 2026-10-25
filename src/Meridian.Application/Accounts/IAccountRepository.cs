using Meridian.Domain;

namespace Meridian.Application.Accounts;

/// <summary>Reads accounts with their positions and securities loaded (valuation needs prices).</summary>
public interface IAccountRepository
{
    Task<IReadOnlyList<Account>> ListForClientAsync(Guid clientId, CancellationToken cancellationToken = default);

    Task<Account?> FindAsync(Guid accountId, CancellationToken cancellationToken = default);
}
