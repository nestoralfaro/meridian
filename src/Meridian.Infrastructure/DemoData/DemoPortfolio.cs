using Meridian.Domain;

namespace Meridian.Infrastructure.DemoData;

internal sealed record DemoDataset(
    IReadOnlyList<Client> Clients,
    IReadOnlyList<Security> Securities,
    IReadOnlyList<Account> Accounts,
    IReadOnlyList<Transaction> Transactions);

/// <summary>
/// The synthetic portfolio behind the demo. Current holdings are defined explicitly (so stories can rely on
/// exact figures); the 90-day activity history comes from a seeded generator, so the same date always yields
/// the same data. Every person, account number and security here is fictional.
/// </summary>
internal static class DemoPortfolio
{
    public const string DanaEmail = "dana@meridian.test";
    public const string RajEmail = "raj@meridian.test";
    public const int HistoryDays = 90;
    private const int Seed = 20261024;

    public static readonly string[] FictionalSymbols = ["MDTOT", "MDINT", "MDBND", "MDMMF", "ACME", "GLBX"];

    public static DemoDataset Generate(DateOnly asOf)
    {
        var random = new Random(Seed);

        var securities = new Dictionary<string, Security>
        {
            ["MDTOT"] = new("MDTOT", "Meridian Total Market Index Fund", AssetClass.Fund, Money.Usd(245.30m), Money.Usd(243.10m)),
            ["MDINT"] = new("MDINT", "Meridian International Index Fund", AssetClass.Fund, Money.Usd(61.45m), Money.Usd(61.90m)),
            ["MDBND"] = new("MDBND", "Meridian Aggregate Bond Fund", AssetClass.Bond, Money.Usd(64.12m), Money.Usd(64.05m)),
            ["MDMMF"] = new("MDMMF", "Meridian Money Market Fund", AssetClass.Cash, Money.Usd(1.00m), Money.Usd(1.00m)),
            ["ACME"] = new("ACME", "Acme Corporation", AssetClass.Stock, Money.Usd(128.75m), Money.Usd(130.00m)),
            ["GLBX"] = new("GLBX", "Globex Industries", AssetClass.Stock, Money.Usd(42.18m), Money.Usd(41.60m)),
        };
        Position Hold(string symbol, decimal quantity, decimal costBasis) => new(securities[symbol], quantity, Money.Usd(costBasis));

        var dana = new Client(NextGuid(random), "Dana Lee", DanaEmail);
        var raj = new Client(NextGuid(random), "Raj Mehta", RajEmail);

        var danaBrokerage = new Account(NextGuid(random), dana.Id, "Individual Brokerage", AccountType.IndividualBrokerage, "****4821",
            Money.Usd(1381.05m), [Hold("MDTOT", 40m, 8800.00m), Hold("ACME", 10m, 1100.00m)]);
        var danaRoth = new Account(NextGuid(random), dana.Id, "Roth IRA", AccountType.RothIra, "****7310",
            Money.Usd(189.20m), [Hold("MDTOT", 20m, 4500.00m), Hold("MDBND", 50m, 3150.00m)]);
        var rajBrokerage = new Account(NextGuid(random), raj.Id, "Individual Brokerage", AccountType.IndividualBrokerage, "****2296",
            Money.Usd(15240.00m), [Hold("MDTOT", 300m, 61200.00m), Hold("MDINT", 400m, 22800.00m), Hold("GLBX", 150m, 5400.00m), Hold("ACME", 60m, 6900.00m)]);
        var rajIra = new Account(NextGuid(random), raj.Id, "Traditional IRA", AccountType.TraditionalIra, "****5508",
            Money.Usd(3120.40m), [Hold("MDBND", 2000m, 127000.00m), Hold("MDTOT", 450m, 88200.00m)]);
        var rajSavings = new Account(NextGuid(random), raj.Id, "High-Yield Savings", AccountType.Savings, "****0917",
            Money.Usd(48000.00m), []);

        var history = new HistoryBuilder(random, asOf);
        history.Monthly(danaBrokerage, TransactionType.Deposit, "Deposit from linked checking", 500m, jitter: 0m);
        history.Monthly(danaBrokerage, TransactionType.Buy, "Bought MDTOT", -490.60m, jitter: 0m, symbol: "MDTOT", quantity: 2m, dayOffset: 2);
        history.Once(danaBrokerage, TransactionType.Dividend, "ACME quarterly dividend", 4.10m, symbol: "ACME");
        history.Monthly(danaRoth, TransactionType.Deposit, "Roth IRA contribution", 250m, jitter: 0m);
        history.Monthly(danaRoth, TransactionType.Buy, "Bought MDBND", -192.36m, jitter: 0m, symbol: "MDBND", quantity: 3m, dayOffset: 3);
        history.Monthly(rajBrokerage, TransactionType.Dividend, "MDTOT dividend reinvestment", 182m, jitter: 25m, symbol: "MDTOT");
        history.Once(rajBrokerage, TransactionType.Sell, "Sold GLBX", 2109.00m, symbol: "GLBX", quantity: 50m);
        history.Once(rajBrokerage, TransactionType.TransferOut, "Transfer to High-Yield Savings", -5000m);
        history.Monthly(rajIra, TransactionType.Dividend, "MDBND monthly distribution", 410m, jitter: 30m, symbol: "MDBND");
        history.Monthly(rajSavings, TransactionType.Interest, "Interest paid", 172m, jitter: 6m);
        history.Once(rajSavings, TransactionType.TransferIn, "Transfer from Individual Brokerage", 5000m);

        return new DemoDataset(
            [dana, raj],
            [.. securities.Values],
            [danaBrokerage, danaRoth, rajBrokerage, rajIra, rajSavings],
            history.Build());
    }

    private static Guid NextGuid(Random random)
    {
        Span<byte> bytes = stackalloc byte[16];
        random.NextBytes(bytes);
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x40); // version 4 layout
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // RFC 4122 variant
        return new Guid(bytes);
    }

    private sealed class HistoryBuilder(Random random, DateOnly asOf)
    {
        private readonly List<Transaction> _transactions = [];
        private readonly DateOnly _from = asOf.AddDays(-(HistoryDays - 1));

        /// <summary>One transaction per month in the window, on a seeded day near the start of the month.</summary>
        public void Monthly(Account account, TransactionType type, string description, decimal amount, decimal jitter,
            string? symbol = null, decimal? quantity = null, int dayOffset = 0)
        {
            for (var month = new DateOnly(_from.Year, _from.Month, 1); month <= asOf; month = month.AddMonths(1))
            {
                var date = month.AddDays(random.Next(0, 3) + dayOffset);
                var value = amount + (jitter == 0m ? 0m : Math.Round((decimal)(random.NextDouble() * 2 - 1) * jitter, 2));
                Add(account, date, type, description, value, symbol, quantity);
            }
        }

        /// <summary>One transaction on a seeded day within the window.</summary>
        public void Once(Account account, TransactionType type, string description, decimal amount, string? symbol = null, decimal? quantity = null) =>
            Add(account, _from.AddDays(random.Next(0, HistoryDays)), type, description, amount, symbol, quantity);

        public IReadOnlyList<Transaction> Build() => [.. _transactions.OrderBy(t => t.Date).ThenBy(t => t.Id)];

        private void Add(Account account, DateOnly date, TransactionType type, string description, decimal amount, string? symbol, decimal? quantity)
        {
            var id = NextGuid(random); // drawn unconditionally so the sequence doesn't depend on which dates fall in the window
            if (date >= _from && date <= asOf)
            {
                _transactions.Add(new Transaction(id, account.Id, date, type, description, Money.Usd(amount), symbol, quantity));
            }
        }
    }
}
