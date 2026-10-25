namespace Meridian.Application.Auditing;

/// <summary>An immutable record of a security- or business-relevant event.</summary>
/// <param name="Actor">Who: e.g. <c>user:{id}</c>, or <c>anonymous</c>.</param>
/// <param name="Action">What: e.g. <c>SignedIn</c>, <c>SignInFailed</c>, <c>AccessDenied</c>.</param>
/// <param name="Subject">What it was about: e.g. <c>user:{id}</c> or <c>account:{id}</c>.</param>
public sealed record AuditRecord(string Actor, string Action, string Subject);

/// <summary>Append-only audit trail (the database refuses updates and deletes for the app role).</summary>
public interface IAuditTrail
{
    Task RecordAsync(AuditRecord record, CancellationToken cancellationToken = default);
}
