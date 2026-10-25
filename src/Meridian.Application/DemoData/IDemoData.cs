namespace Meridian.Application.DemoData;

/// <summary>Keeps the synthetic demo portfolio present and fresh.</summary>
public interface IDemoData
{
    /// <summary>Seeds the demo portfolio if the database has none. Safe to call concurrently and repeatedly.</summary>
    Task EnsureSeededAsync(CancellationToken cancellationToken = default);

    /// <summary>Replaces all demo data with a freshly generated portfolio (the audit trail is never touched).</summary>
    Task ResetAsync(CancellationToken cancellationToken = default);

    /// <summary>When the demo data was last seeded or reset; <c>null</c> if never.</summary>
    Task<DateTimeOffset?> LastResetAtAsync(CancellationToken cancellationToken = default);
}
