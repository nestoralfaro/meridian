namespace Meridian.Infrastructure.Persistence;

/// <summary>
/// An immutable record of a state change (who, what, when, correlated to the request).
/// The table is append-only: the application role may insert and read, never update or delete.
/// </summary>
internal sealed class AuditEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTimeOffset OccurredAt { get; init; }
    public required string Actor { get; init; }
    public required string Action { get; init; }
    public required string Subject { get; init; }
    public string? CorrelationId { get; init; }
    public string? Data { get; init; }
}
