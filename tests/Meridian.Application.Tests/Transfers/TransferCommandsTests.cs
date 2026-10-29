using Meridian.Application.Auditing;
using Meridian.Application.Transfers;
using Meridian.Domain;

namespace Meridian.Application.Tests.Transfers;

public sealed class TransferCommandsTests
{
    private static readonly Guid Dana = Guid.NewGuid();
    private static readonly Guid Raj = Guid.NewGuid();

    private readonly Account _brokerage = new(Guid.NewGuid(), Dana, "Individual Brokerage", AccountType.IndividualBrokerage, "****4821", Money.Usd(1381.05m), []);
    private readonly Account _roth = new(Guid.NewGuid(), Dana, "Roth IRA", AccountType.RothIra, "****7310", Money.Usd(189.20m), []);
    private readonly Account _rajSavings = new(Guid.NewGuid(), Raj, "High-Yield Savings", AccountType.Savings, "****0917", Money.Usd(48000m), []);
    private readonly InMemoryTransferStore _store;
    private readonly TransferCommands _commands;

    public TransferCommandsTests()
    {
        _store = new InMemoryTransferStore(_brokerage, _roth, _rajSavings);
        _commands = new TransferCommands(_store, new FixedClock());
    }

    private TransferRequest Request(decimal amount = 500m, string key = "key-1", Guid? from = null, Guid? to = null, uint? version = null) =>
        new(Dana, from ?? _brokerage.Id, to ?? _roth.Id, Money.Usd(amount), key, version ?? _brokerage.Version);

    private Task<TransferOutcome> Send(TransferRequest request) =>
        _commands.TransferAsync(request, "user:dana", TestContext.Current.CancellationToken);

    [Fact]
    public async Task MovesTheCashAndAuditsIt()
    {
        var outcome = await Send(Request());

        outcome.Status.ShouldBe(TransferStatus.Completed);
        outcome.Transfer!.Amount.ShouldBe(Money.Usd(500m));
        _brokerage.Cash.ShouldBe(Money.Usd(881.05m));
        _roth.Cash.ShouldBe(Money.Usd(689.20m));
        _store.Saved.ShouldHaveSingleItem().Audit.ShouldBe(new AuditRecord("user:dana", "TransferCompleted", $"transfer:{outcome.Transfer.Id}"));
    }

    [Fact]
    public async Task RetryingWithTheSameKeyReturnsTheOriginalAndMovesNothing()
    {
        var first = await Send(Request());
        var retry = await Send(Request());

        retry.Status.ShouldBe(TransferStatus.Replayed);
        retry.Transfer.ShouldBe(first.Transfer);
        _brokerage.Cash.ShouldBe(Money.Usd(881.05m)); // moved once
        _store.Saved.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ReusingAKeyForADifferentTransferIsRefused()
    {
        await Send(Request(amount: 500m));

        var outcome = await Send(Request(amount: 600m));

        outcome.Status.ShouldBe(TransferStatus.KeyReused);
        _brokerage.Cash.ShouldBe(Money.Usd(881.05m));
    }

    [Fact]
    public async Task InsufficientCashMovesNothingAndSaysWhatsAvailable()
    {
        var outcome = await Send(Request(amount: 1381.06m));

        outcome.Status.ShouldBe(TransferStatus.InsufficientCash);
        outcome.AvailableCash.ShouldBe(Money.Usd(1381.05m));
        _store.Saved.ShouldBeEmpty();
    }

    [Fact]
    public async Task AStaleReviewIsAConflict()
    {
        var outcome = await Send(Request(version: _brokerage.Version + 1));

        outcome.Status.ShouldBe(TransferStatus.Conflict);
        _brokerage.Cash.ShouldBe(Money.Usd(1381.05m));
    }

    [Fact]
    public async Task AConcurrentChangeWhileSavingIsAConflict()
    {
        _store.FailNextSaveWith = new TransferConcurrencyException();

        var outcome = await Send(Request());

        outcome.Status.ShouldBe(TransferStatus.Conflict);
    }

    [Fact]
    public async Task LosingARaceOnTheSameKeyAnswersAsARetryOfTheWinner()
    {
        var winner = await Send(Request(key: "race"));
        _store.HideKeysOnce = true; // the second request doesn't see the first until its insert collides
        _store.FailNextSaveWith = new DuplicateIdempotencyKeyException();

        var loser = await Send(Request(key: "race", version: _brokerage.Version));

        loser.Status.ShouldBe(TransferStatus.Replayed);
        loser.Transfer.ShouldBe(winner.Transfer);
    }

    [Fact]
    public async Task AnotherClientsAccountLooksMissingButIsFlaggedForAudit()
    {
        var outcome = await Send(Request(to: _rajSavings.Id));

        outcome.Status.ShouldBe(TransferStatus.AccountNotFound);
        outcome.ForeignAccountId.ShouldBe(_rajSavings.Id);
        _rajSavings.Cash.ShouldBe(Money.Usd(48000m));
    }

    [Fact]
    public async Task AnUnknownAccountIsNotFound()
    {
        (await Send(Request(to: Guid.NewGuid()))).ShouldBe(new TransferOutcome(TransferStatus.AccountNotFound));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10.005)]
    [InlineData(1_000_000.01)]
    public async Task RejectsInvalidAmounts(decimal amount)
    {
        (await Send(Request(amount: amount))).Status.ShouldBe(TransferStatus.InvalidAmount);
    }

    [Fact]
    public async Task RejectsTransfersToTheSameAccount()
    {
        (await Send(Request(to: _brokerage.Id))).Status.ShouldBe(TransferStatus.SameAccount);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 29, 15, 0, 0, TimeSpan.Zero);
    }

    private sealed class InMemoryTransferStore(params Account[] accounts) : ITransferStore
    {
        private readonly List<Transfer> _transfers = [];

        public List<(Transfer Transfer, Transaction Out, Transaction In, AuditRecord Audit)> Saved { get; } = [];
        public Exception? FailNextSaveWith { get; set; }
        public bool HideKeysOnce { get; set; }

        public Task<Transfer?> FindByIdempotencyKeyAsync(Guid clientId, string idempotencyKey, CancellationToken cancellationToken = default)
        {
            if (HideKeysOnce)
            {
                HideKeysOnce = false;
                return Task.FromResult<Transfer?>(null);
            }

            return Task.FromResult(_transfers.SingleOrDefault(t => t.ClientId == clientId && t.IdempotencyKey == idempotencyKey));
        }

        public Task<Account?> LoadAccountAsync(Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(accounts.SingleOrDefault(a => a.Id == accountId));

        public Task SaveAsync(Transfer transfer, Transaction outgoing, Transaction incoming, AuditRecord audit, CancellationToken cancellationToken = default)
        {
            if (FailNextSaveWith is { } failure)
            {
                FailNextSaveWith = null;
                throw failure;
            }

            _transfers.Add(transfer);
            Saved.Add((transfer, outgoing, incoming, audit));
            return Task.CompletedTask;
        }
    }
}
