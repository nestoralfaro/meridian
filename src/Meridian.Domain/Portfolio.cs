namespace Meridian.Domain;

public enum AssetClass
{
    Stock,
    Bond,
    Fund,
    Cash,
}

public enum AccountType
{
    IndividualBrokerage,
    RothIra,
    TraditionalIra,
    Savings,
}

/// <summary>A tradable instrument. All securities in this demo are fictional.</summary>
public sealed class Security
{
    public Security(string symbol, string name, AssetClass assetClass, Money price, Money previousClose)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Symbol = symbol;
        Name = name;
        AssetClass = assetClass;
        Price = price;
        PreviousClose = previousClose;
    }

    private Security() => (Symbol, Name) = (string.Empty, string.Empty); // for EF Core

    public string Symbol { get; private set; }
    public string Name { get; private set; }
    public AssetClass AssetClass { get; private set; }
    public Money Price { get; private set; }
    public Money PreviousClose { get; private set; }
}

/// <summary>A holding of one security in one account.</summary>
public sealed class Position
{
    public Position(Security security, decimal quantity, Money costBasis)
    {
        ArgumentNullException.ThrowIfNull(security);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        Id = Guid.CreateVersion7();
        Security = security;
        SecuritySymbol = security.Symbol;
        Quantity = quantity;
        CostBasis = costBasis;
    }

    private Position() => (Security, SecuritySymbol) = (null!, string.Empty); // for EF Core

    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public string SecuritySymbol { get; private set; }
    public Security Security { get; private set; }
    public decimal Quantity { get; private set; }
    public Money CostBasis { get; private set; }

    public Money MarketValue => (Security.Price * Quantity).RoundToCents();

    public Money DayChange => ((Security.Price - Security.PreviousClose) * Quantity).RoundToCents();

    /// <summary>Market value minus what was paid (not yet realized by a sale).</summary>
    public Money UnrealizedGain => MarketValue - CostBasis;

    /// <summary>Unrealized gain as a percentage of cost basis, to two decimal places; null when the cost basis is zero.</summary>
    public decimal? UnrealizedGainPercent =>
        CostBasis.Amount == 0m ? null : Math.Round(UnrealizedGain.Amount / CostBasis.Amount * 100m, 2, MidpointRounding.ToEven);
}

public sealed class Account
{
    private readonly List<Position> _positions = [];

    public Account(Guid id, Guid clientId, string name, AccountType type, string maskedNumber, Money cash, IEnumerable<Position> positions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(maskedNumber);
        ArgumentNullException.ThrowIfNull(positions);
        Id = id;
        ClientId = clientId;
        Name = name;
        Type = type;
        MaskedNumber = maskedNumber;
        Cash = cash;
        _positions.AddRange(positions);
    }

    private Account() => (Name, MaskedNumber) = (string.Empty, string.Empty); // for EF Core

    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public string Name { get; private set; }
    public AccountType Type { get; private set; }

    /// <summary>Only the last four digits are ever stored or shown.</summary>
    public string MaskedNumber { get; private set; }

    public Money Cash { get; private set; }
    public IReadOnlyList<Position> Positions => _positions;

    /// <summary>Changes whenever the account row changes (PostgreSQL <c>xmin</c>); used for optimistic concurrency.</summary>
    public uint Version { get; private set; }

    /// <summary>Removes cash. Callers check <see cref="Cash"/> first; overdrawing is a bug, not a business outcome.</summary>
    public void Withdraw(Money amount)
    {
        if (amount.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amounts must be positive.");
        }

        if (amount > Cash)
        {
            throw new InvalidOperationException($"Account {Id} has {Cash} available; can't withdraw {amount}.");
        }

        Cash -= amount;
    }

    public void Deposit(Money amount)
    {
        if (amount.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amounts must be positive.");
        }

        Cash += amount;
    }

    public Money TotalValue => Cash + _positions.Select(p => p.MarketValue).Sum(Cash.Currency);

    public Money DayChange => _positions.Select(p => p.DayChange).Sum(Cash.Currency);

    /// <summary>Day change as a percentage of yesterday's value, to two decimal places.</summary>
    public decimal DayChangePercent
    {
        get
        {
            var previous = (TotalValue - DayChange).Amount;
            return previous == 0m ? 0m : Math.Round(DayChange.Amount / previous * 100m, 2, MidpointRounding.ToEven);
        }
    }
}

public sealed class Client
{
    public Client(Guid id, string displayName, string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        Id = id;
        DisplayName = displayName;
        Email = email;
    }

    private Client() => (DisplayName, Email) = (string.Empty, string.Empty); // for EF Core

    public Guid Id { get; private set; }
    public string DisplayName { get; private set; }
    public string Email { get; private set; }
}

public enum TransactionType
{
    Deposit,
    Withdrawal,
    Buy,
    Sell,
    Dividend,
    Interest,
    TransferIn,
    TransferOut,
}

/// <summary>A completed account activity. Positive amounts add cash; negative amounts remove it.</summary>
public sealed class Transaction
{
    public Transaction(Guid id, Guid accountId, DateOnly date, TransactionType type, string description, Money amount, string? symbol = null, decimal? quantity = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Id = id;
        AccountId = accountId;
        Date = date;
        Type = type;
        Description = description;
        Amount = amount;
        Symbol = symbol;
        Quantity = quantity;
    }

    private Transaction() => Description = string.Empty; // for EF Core

    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public DateOnly Date { get; private set; }
    public TransactionType Type { get; private set; }
    public string Description { get; private set; }
    public Money Amount { get; private set; }
    public string? Symbol { get; private set; }
    public decimal? Quantity { get; private set; }
}

/// <summary>Cash moved between two accounts owned by the same client, recorded once per idempotency key.</summary>
public sealed class Transfer
{
    public const int MaxIdempotencyKeyLength = 64;

    private Transfer(Guid clientId, Guid fromAccountId, Guid toAccountId, Money amount, string idempotencyKey, DateTimeOffset requestedAt)
    {
        Id = Guid.CreateVersion7();
        ClientId = clientId;
        FromAccountId = fromAccountId;
        ToAccountId = toAccountId;
        Amount = amount;
        IdempotencyKey = idempotencyKey;
        RequestedAt = requestedAt;
    }

    private Transfer() => IdempotencyKey = string.Empty; // for EF Core

    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid FromAccountId { get; private set; }
    public Guid ToAccountId { get; private set; }
    public Money Amount { get; private set; }
    public string IdempotencyKey { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }

    /// <summary>
    /// Moves <paramref name="amount"/> and returns the transfer with the activity it adds to each account.
    /// Business rules (ownership, sufficient cash) are checked by the use case first; violations here are bugs.
    /// </summary>
    public static (Transfer Transfer, Transaction Out, Transaction In) Between(
        Account from, Account to, Money amount, string idempotencyKey, DateTimeOffset requestedAt)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        if (from.Id == to.Id)
        {
            throw new ArgumentException("A transfer needs two different accounts.", nameof(to));
        }

        if (from.ClientId != to.ClientId)
        {
            throw new ArgumentException("Transfers are only between one client's own accounts.", nameof(to));
        }

        if (amount.Amount != decimal.Round(amount.Amount, 2))
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Transfers are in whole cents.");
        }

        from.Withdraw(amount);
        to.Deposit(amount);

        var transfer = new Transfer(from.ClientId, from.Id, to.Id, amount, idempotencyKey, requestedAt);
        var date = DateOnly.FromDateTime(requestedAt.UtcDateTime);
        var @out = new Transaction(Guid.CreateVersion7(), from.Id, date, TransactionType.TransferOut, $"Transfer to {to.Name}", -amount);
        var @in = new Transaction(Guid.CreateVersion7(), to.Id, date, TransactionType.TransferIn, $"Transfer from {from.Name}", amount);
        return (transfer, @out, @in);
    }
}
