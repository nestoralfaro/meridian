namespace Meridian.Domain.Tests;

public sealed class TransferTests
{
    private static readonly Guid Dana = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 15, 30, 0, TimeSpan.Zero);

    private static Account Brokerage(decimal cash = 1381.05m) =>
        new(Guid.NewGuid(), Dana, "Individual Brokerage", AccountType.IndividualBrokerage, "****4821", Money.Usd(cash), []);

    private static Account Roth(Guid? client = null) =>
        new(Guid.NewGuid(), client ?? Dana, "Roth IRA", AccountType.RothIra, "****7310", Money.Usd(189.20m), []);

    [Fact]
    public void MovesCashAndRecordsActivityOnBothAccounts()
    {
        var (from, to) = (Brokerage(), Roth());

        var (transfer, @out, @in) = Transfer.Between(from, to, Money.Usd(500m), "key-1", Now);

        from.Cash.ShouldBe(Money.Usd(881.05m));
        to.Cash.ShouldBe(Money.Usd(689.20m));
        (transfer.ClientId, transfer.FromAccountId, transfer.ToAccountId, transfer.Amount).ShouldBe((Dana, from.Id, to.Id, Money.Usd(500m)));
        (@out.AccountId, @out.Type, @out.Amount, @out.Description).ShouldBe((from.Id, TransactionType.TransferOut, Money.Usd(-500m), "Transfer to Roth IRA"));
        (@in.AccountId, @in.Type, @in.Amount, @in.Description).ShouldBe((to.Id, TransactionType.TransferIn, Money.Usd(500m), "Transfer from Individual Brokerage"));
        @out.Date.ShouldBe(new DateOnly(2026, 10, 29));
    }

    [Fact]
    public void CanMoveEveryLastCent()
    {
        var from = Brokerage();

        Transfer.Between(from, Roth(), Money.Usd(1381.05m), "key", Now);

        from.Cash.ShouldBe(Money.Usd(0m));
    }

    [Fact]
    public void NeverOverdrawsAndLeavesBothAccountsUntouched()
    {
        var (from, to) = (Brokerage(), Roth());

        Should.Throw<InvalidOperationException>(() => Transfer.Between(from, to, Money.Usd(1381.06m), "key", Now));

        from.Cash.ShouldBe(Money.Usd(1381.05m));
        to.Cash.ShouldBe(Money.Usd(189.20m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.001)]
    public void RejectsAmountsThatArentPositiveWholeCents(decimal amount)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Transfer.Between(Brokerage(), Roth(), Money.Usd(amount), "key", Now));
    }

    [Fact]
    public void OnlyBetweenTwoDifferentAccountsOfOneClient()
    {
        var brokerage = Brokerage();

        Should.Throw<ArgumentException>(() => Transfer.Between(brokerage, brokerage, Money.Usd(1m), "key", Now));
        Should.Throw<ArgumentException>(() => Transfer.Between(brokerage, Roth(client: Guid.NewGuid()), Money.Usd(1m), "key", Now));
    }
}
