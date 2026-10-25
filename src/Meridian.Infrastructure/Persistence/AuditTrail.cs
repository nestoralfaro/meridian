using System.Diagnostics;
using Meridian.Application.Auditing;

namespace Meridian.Infrastructure.Persistence;

/// <summary>Appends to "AuditEvents", correlated with the request's trace id (searchable in Application Insights).</summary>
internal sealed class AuditTrail(MeridianDbContext db, TimeProvider clock) : IAuditTrail
{
    public async Task RecordAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            OccurredAt = clock.GetUtcNow(),
            Actor = record.Actor,
            Action = record.Action,
            Subject = record.Subject,
            CorrelationId = Activity.Current?.TraceId.ToHexString(),
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
