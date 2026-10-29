using System.Collections.Concurrent;
using Meridian.Domain;

namespace Meridian.Application.Quotes;

/// <summary>Reads the securities whose prices the demo simulates.</summary>
public interface ISecurityCatalog
{
    Task<IReadOnlyList<Security>> ListAsync(CancellationToken cancellationToken = default);
}

public sealed record LiveQuote(string Symbol, Money Price, Money PreviousClose);

public sealed record LiveHolding(string Symbol, decimal Quantity, Money CostBasis);

/// <summary>A holding re-valued at the live price, with the same domain rules (and rounding) as stored balances.</summary>
public sealed record LivePosition(string Symbol, Money Price, Money MarketValue, Money DayChange, Money UnrealizedGain, decimal? UnrealizedGainPercent);

/// <summary>An account re-valued at live prices: cash plus holdings, computed on the server.</summary>
public sealed record LiveValuation(Money TotalValue, Money DayChange, IReadOnlyList<LivePosition> Positions);

/// <summary>
/// Indicative intraday prices for the demo (a bounded random walk from the last close). Balances in the database stay
/// valued at the close; these are display-only, the way portals show live quotes. One board per process.
/// </summary>
public sealed class QuoteBoard
{
    private readonly ConcurrentDictionary<string, Security> _securities = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, LiveQuote> _quotes = new(StringComparer.Ordinal);
    private readonly Lock _tickLock = new();

    public bool IsLoaded => !_quotes.IsEmpty;

    public void Load(IEnumerable<Security> securities)
    {
        ArgumentNullException.ThrowIfNull(securities);
        foreach (var security in securities)
        {
            _securities[security.Symbol] = security;
            _quotes.TryAdd(security.Symbol, new LiveQuote(security.Symbol, security.Price, security.PreviousClose));
        }
    }

    /// <summary>Moves every price one step.</summary>
    public void Tick(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        lock (_tickLock)
        {
            foreach (var (symbol, quote) in _quotes)
            {
                _quotes[symbol] = quote with { Price = PriceWalk.Next(quote.Price, quote.PreviousClose, random.NextDouble()) };
            }
        }
    }

    public LiveQuote? Quote(string symbol) => _quotes.GetValueOrDefault(symbol);

    /// <summary>Values holdings at live prices; symbols without a quote are skipped.</summary>
    public IReadOnlyList<LivePosition> Value(IEnumerable<LiveHolding> holdings)
    {
        ArgumentNullException.ThrowIfNull(holdings);
        var values = new List<LivePosition>();
        foreach (var holding in holdings)
        {
            if (_quotes.TryGetValue(holding.Symbol, out var quote) && _securities.TryGetValue(holding.Symbol, out var security))
            {
                var live = new Position(new Security(holding.Symbol, security.Name, security.AssetClass, quote.Price, quote.PreviousClose), holding.Quantity, holding.CostBasis);
                values.Add(new LivePosition(holding.Symbol, quote.Price, live.MarketValue, live.DayChange, live.UnrealizedGain, live.UnrealizedGainPercent));
            }
        }

        return values;
    }

    /// <summary>Cash plus holdings at live prices.</summary>
    public LiveValuation ValueAccount(Money cash, IReadOnlyList<LiveHolding> holdings)
    {
        var positions = Value(holdings);
        return new LiveValuation(
            cash + positions.Select(p => p.MarketValue).Sum(cash.Currency),
            positions.Select(p => p.DayChange).Sum(cash.Currency),
            positions);
    }
}
