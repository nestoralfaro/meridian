namespace Meridian.Domain;

/// <summary>
/// An amount of money in one currency. Always <see cref="decimal"/> (never binary floating point);
/// arithmetic across currencies is refused, and rounding to cents is explicit (banker's rounding).
/// </summary>
public readonly record struct Money
{
    public Money(decimal amount, string currency)
    {
        if (currency is not { Length: 3 } || !currency.All(char.IsAsciiLetterUpper))
        {
            throw new ArgumentException($"'{currency}' is not an upper-case ISO 4217 currency code.", nameof(currency));
        }

        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public static Money Usd(decimal amount) => new(amount, "USD");

    public static Money Zero(string currency) => new(0m, currency);

    /// <summary>Rounds to whole cents; ties go to the even cent (MidpointRounding.ToEven), avoiding upward bias in sums.</summary>
    public Money RoundToCents() => new(Math.Round(Amount, 2, MidpointRounding.ToEven), Currency);

    public static Money operator +(Money left, Money right) => new(left.Amount + SameCurrency(left, right).Amount, left.Currency);

    public static Money operator -(Money left, Money right) => new(left.Amount - SameCurrency(left, right).Amount, left.Currency);

    public static Money operator *(Money money, decimal factor) => new(money.Amount * factor, money.Currency);

    public static Money operator -(Money money) => new(-money.Amount, money.Currency);

    public static bool operator <(Money left, Money right) => left.Amount < SameCurrency(left, right).Amount;

    public static bool operator >(Money left, Money right) => left.Amount > SameCurrency(left, right).Amount;

    public static bool operator <=(Money left, Money right) => left.Amount <= SameCurrency(left, right).Amount;

    public static bool operator >=(Money left, Money right) => left.Amount >= SameCurrency(left, right).Amount;

    public static Money Add(Money left, Money right) => left + right;

    public static Money Subtract(Money left, Money right) => left - right;

    public static Money Multiply(Money money, decimal factor) => money * factor;

    public static Money Negate(Money money) => -money;

    public int CompareTo(Money other) => Amount.CompareTo(SameCurrency(this, other).Amount);

    public override string ToString() => $"{Amount.ToString("0.00##", System.Globalization.CultureInfo.InvariantCulture)} {Currency}";

    private static Money SameCurrency(Money left, Money right) =>
        left.Currency == right.Currency
            ? right
            : throw new InvalidOperationException($"Cannot combine {left.Currency} and {right.Currency} amounts.");
}

public static class MoneyExtensions
{
    /// <summary>Sums amounts that must all be in <paramref name="currency"/>; an empty sequence is zero.</summary>
    public static Money Sum(this IEnumerable<Money> amounts, string currency) =>
        amounts.Aggregate(Money.Zero(currency), (total, amount) => total + amount);
}
