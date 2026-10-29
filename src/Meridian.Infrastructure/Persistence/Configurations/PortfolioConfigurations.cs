using Meridian.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meridian.Infrastructure.Persistence.Configurations;

/// <summary>Money is stored as an exact numeric amount plus its ISO currency code (an EF Core complex type).</summary>
internal static class MoneyMapping
{
    public static void Map<T>(ComplexPropertyBuilder<T> money, string prefix) where T : struct
    {
        money.Property("Amount").HasColumnName($"{prefix}Amount").HasPrecision(19, 4);
        money.Property("Currency").HasColumnName($"{prefix}Currency").HasMaxLength(3).IsFixedLength();
    }
}

internal sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> client)
    {
        client.ToTable("Clients");
        client.HasKey(c => c.Id);
        client.Property(c => c.DisplayName).HasMaxLength(100);
        client.Property(c => c.Email).HasMaxLength(200);
        client.HasIndex(c => c.Email).IsUnique();
    }
}

internal sealed class SecurityConfiguration : IEntityTypeConfiguration<Security>
{
    public void Configure(EntityTypeBuilder<Security> security)
    {
        security.ToTable("Securities");
        security.HasKey(s => s.Symbol);
        security.Property(s => s.Symbol).HasMaxLength(10);
        security.Property(s => s.Name).HasMaxLength(200);
        security.Property(s => s.AssetClass).HasConversion<string>().HasMaxLength(20);
        security.ComplexProperty(s => s.Price, m => MoneyMapping.Map(m, "Price"));
        security.ComplexProperty(s => s.PreviousClose, m => MoneyMapping.Map(m, "PreviousClose"));
    }
}

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> account)
    {
        account.ToTable("Accounts");
        account.HasKey(a => a.Id);
        account.Property(a => a.Name).HasMaxLength(100);
        account.Property(a => a.Type).HasConversion<string>().HasMaxLength(30);
        account.Property(a => a.MaskedNumber).HasMaxLength(12);
        account.ComplexProperty(a => a.Cash, m => MoneyMapping.Map(m, "Cash"));
        account.HasOne<Client>().WithMany().HasForeignKey(a => a.ClientId).OnDelete(DeleteBehavior.Cascade);
        account.HasMany(a => a.Positions).WithOne().HasForeignKey(p => p.AccountId).OnDelete(DeleteBehavior.Cascade);
        account.Navigation(a => a.Positions).UsePropertyAccessMode(PropertyAccessMode.Field);
        account.HasIndex(a => a.ClientId);

        // PostgreSQL's xmin system column: changes on every update, so EF Core adds "AND xmin = @version" to UPDATEs.
        account.Property(a => a.Version).IsRowVersion();

        // Defence in depth: the use case checks available cash, and the database refuses an overdraft regardless.
        account.ToTable(t => t.HasCheckConstraint("CK_Accounts_Cash_NotNegative", "\"CashAmount\" >= 0"));
        account.Ignore(a => a.TotalValue);
        account.Ignore(a => a.DayChange);
        account.Ignore(a => a.DayChangePercent);
    }
}

internal sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> position)
    {
        position.ToTable("Positions");
        position.HasKey(p => p.Id);
        position.Property(p => p.SecuritySymbol).HasMaxLength(10);
        position.Property(p => p.Quantity).HasPrecision(19, 6);
        position.ComplexProperty(p => p.CostBasis, m => MoneyMapping.Map(m, "CostBasis"));
        position.HasOne(p => p.Security).WithMany().HasForeignKey(p => p.SecuritySymbol).OnDelete(DeleteBehavior.Restrict);
        position.Ignore(p => p.MarketValue);
        position.Ignore(p => p.DayChange);
    }
}

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> transaction)
    {
        transaction.ToTable("Transactions");
        transaction.HasKey(t => t.Id);
        transaction.Property(t => t.Type).HasConversion<string>().HasMaxLength(20);
        transaction.Property(t => t.Description).HasMaxLength(200);
        transaction.Property(t => t.Symbol).HasMaxLength(10);
        transaction.Property(t => t.Quantity).HasPrecision(19, 6);
        transaction.ComplexProperty(t => t.Amount, m => MoneyMapping.Map(m, "Amount"));
        transaction.HasOne<Account>().WithMany().HasForeignKey(t => t.AccountId).OnDelete(DeleteBehavior.Cascade);
        transaction.HasIndex(t => new { t.AccountId, t.Date });
    }
}

internal sealed class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> transfer)
    {
        transfer.ToTable("Transfers", t => t.HasCheckConstraint("CK_Transfers_Amount_Positive", "\"AmountAmount\" > 0"));
        transfer.HasKey(t => t.Id);
        transfer.Property(t => t.IdempotencyKey).HasMaxLength(Transfer.MaxIdempotencyKeyLength);
        transfer.ComplexProperty(t => t.Amount, m => MoneyMapping.Map(m, "Amount"));
        transfer.HasOne<Client>().WithMany().HasForeignKey(t => t.ClientId).OnDelete(DeleteBehavior.Cascade);
        transfer.HasOne<Account>().WithMany().HasForeignKey(t => t.FromAccountId).OnDelete(DeleteBehavior.Cascade);
        transfer.HasOne<Account>().WithMany().HasForeignKey(t => t.ToAccountId).OnDelete(DeleteBehavior.Cascade);

        // One transfer per client and key: the database settles races between concurrent retries.
        transfer.HasIndex(t => new { t.ClientId, t.IdempotencyKey }).IsUnique();
    }
}

/// <summary>Single-row table recording when the demo data was last (re)seeded.</summary>
internal sealed class DemoDataState
{
    public int Id { get; set; } = 1;
    public DateTimeOffset LastResetAt { get; set; }

    /// <summary>The <see cref="DemoData.DemoPortfolio.Version"/> that produced the current data.</summary>
    public int DatasetVersion { get; set; }
}

internal sealed class DemoDataStateConfiguration : IEntityTypeConfiguration<DemoDataState>
{
    public void Configure(EntityTypeBuilder<DemoDataState> state)
    {
        state.ToTable("DemoDataState");
        state.HasKey(s => s.Id);
        state.Property(s => s.Id).ValueGeneratedNever();
    }
}
