namespace Daymark.Core;

public enum CalendarProviderKind { Google, Microsoft }
public sealed record RemoteEventIdentity(CalendarProviderKind Provider, string AccountId, string CalendarId, string EventId)
{
    public void Validate()
    {
        if (!Enum.IsDefined(Provider) || string.IsNullOrWhiteSpace(AccountId) || string.IsNullOrWhiteSpace(CalendarId) || string.IsNullOrWhiteSpace(EventId))
            throw new InvalidDataException("A provider identity needs its account, calendar and event identifiers.");
    }
}
public sealed record RemoteCalendar(string Id, string Name, bool CanWrite, string? Color = null);
public sealed record RemoteObservation(CalendarEvent? Event, string? Version, bool Deleted = false)
{
    public void Validate()
    {
        if (Deleted == (Event is not null)) throw new InvalidDataException("A remote observation must contain an event or an explicit deletion.");
        Event?.Validate();
    }
}
public enum SyncAction { None, ImportRemote, UpdateLocal, DeleteLocal, CreateRemote, UpdateRemote, DeleteRemote, Converged, Conflict }
public sealed record SyncDecision(SyncAction Action, CalendarEvent? Local, CalendarEvent? Remote, string? ExpectedRemoteVersion = null);

// Adapters normalize provider data to stable local IDs before reconciliation. An absent
// delta entry means unchanged; it must never be supplied as a deletion observation.
public static class SyncMerge
{
    public static SyncDecision Decide(CalendarEvent? baseline, CalendarEvent? local, RemoteObservation remote)
    {
        baseline?.Validate(); local?.Validate(); remote.Validate();
        if (baseline is not null && local is not null && baseline.Id != local.Id ||
            remote.Event is not null && (local ?? baseline) is { } known && known.Id != remote.Event.Id)
            throw new InvalidDataException("Only explicitly mapped identities can be reconciled.");
        SyncDecision Plan(SyncAction action) => new(action, local, remote.Event,
            action is SyncAction.UpdateRemote or SyncAction.DeleteRemote ? remote.Version : null);
        SyncDecision Write(SyncAction action) => string.IsNullOrWhiteSpace(remote.Version) ? Plan(SyncAction.Conflict) : Plan(action);
        if (baseline is null)
        {
            if (local is null) return Plan(remote.Deleted ? SyncAction.None : SyncAction.ImportRemote);
            if (remote.Deleted) return Plan(SyncAction.CreateRemote);
            return Plan(local == remote.Event ? SyncAction.Converged : SyncAction.Conflict);
        }
        var localChanged = local != baseline;
        var remoteChanged = remote.Deleted || remote.Event != baseline;
        if (!localChanged && !remoteChanged) return Plan(SyncAction.None);
        if (!localChanged) return Plan(remote.Deleted ? SyncAction.DeleteLocal : SyncAction.UpdateLocal);
        if (!remoteChanged) return Write(local is null ? SyncAction.DeleteRemote : SyncAction.UpdateRemote);
        return Plan(local == remote.Event ? SyncAction.Converged : SyncAction.Conflict);
    }
}

public enum SyncFailureAction { RetryLater, SignInRequired, PermissionRequired, Reconcile, FullResync, Stop }
public sealed record SyncFailurePlan(SyncFailureAction Action, TimeSpan? Delay = null);
public static class SyncFailures
{
    public static SyncFailurePlan Classify(int status, int attempt, TimeSpan? retryAfter = null, string? reason = null)
    {
        if (attempt < 0 || retryAfter < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(attempt));
        if (status == 401) return new(SyncFailureAction.SignInRequired);
        if (status == 410) return new(SyncFailureAction.FullResync);
        if (status is 404 or 409 or 412) return new(SyncFailureAction.Reconcile);
        var quota = status == 403 && reason is "rateLimitExceeded" or "userRateLimitExceeded";
        if (status == 403 && !quota) return new(SyncFailureAction.PermissionRequired);
        if (status is 408 or 429 or >= 500 and <= 599 || quota)
        {
            if (attempt >= 6) return new(SyncFailureAction.Stop);
            // Retry-After is a minimum, never shortened by the exponential-backoff cap.
            var backoff = TimeSpan.FromSeconds(Math.Min(300, Math.Pow(2, attempt + 1)));
            return new(SyncFailureAction.RetryLater, retryAfter is { } server && server > backoff ? server : backoff);
        }
        return new(SyncFailureAction.Stop);
    }
}

public sealed record SyncScope(CalendarProviderKind Provider, string AccountId, string CalendarId,
    DateTime? RangeStartUtc = null, DateTime? RangeEndUtc = null, string QueryShape = "events")
{
    public void Validate()
    {
        if (!Enum.IsDefined(Provider) || string.IsNullOrWhiteSpace(AccountId) || string.IsNullOrWhiteSpace(CalendarId) || string.IsNullOrWhiteSpace(QueryShape) ||
            (RangeStartUtc is null) != (RangeEndUtc is null) || RangeStartUtc is not null && (RangeStartUtc.Value.Kind != DateTimeKind.Utc || RangeEndUtc!.Value.Kind != DateTimeKind.Utc || RangeEndUtc <= RangeStartUtc) ||
            Provider == CalendarProviderKind.Microsoft && RangeStartUtc is null)
            throw new InvalidDataException("A sync checkpoint must be scoped to its account, calendar, query and supported UTC date range.");
    }
}
public sealed record RemoteChange(RemoteEventIdentity Identity, RemoteObservation Observation);
public sealed record ChangePage(IReadOnlyList<RemoteChange> Changes, string? NextPageToken, string? CompletedCursor);
public sealed record SyncCommit(SyncScope Scope, IReadOnlyList<RemoteChange> Changes, string CompletedCursor);

// This is staging, not a network client or durable outbox. The integration layer must persist
// the resulting data, conflict records and checkpoint in one transaction before acknowledging it.
public sealed class SyncPageBatch(SyncScope scope, string? previousCursor)
{
    private readonly Dictionary<RemoteEventIdentity, RemoteChange> changes = [];
    private string? nextPage;
    private string? completed;
    private bool started;
    private bool committed;
    private readonly HashSet<string> pageTokens = new(StringComparer.Ordinal);
    public string? CommittedCursor { get; private set; } = previousCursor;
    public string? NextPageToken => nextPage;
    public void Stage(ChangePage page, string? requestedPageToken = null)
    {
        scope.Validate();
        if (page.Changes is null) throw new InvalidDataException("A sync page is missing its changes.");
        if (committed || completed is not null) throw new InvalidOperationException("This sync round is already complete.");
        if (requestedPageToken != (started ? nextPage : null)) throw new InvalidDataException("Unexpected sync page token.");
        if (string.IsNullOrWhiteSpace(page.NextPageToken) == string.IsNullOrWhiteSpace(page.CompletedCursor))
            throw new InvalidDataException("A sync page must provide either another page or a completed cursor.");
        if (page.NextPageToken is not null && pageTokens.Contains(page.NextPageToken)) throw new InvalidDataException("The sync page token did not advance.");
        if (pageTokens.Count >= 1000 || page.Changes.Count > 100000 || changes.Count + page.Changes.Count > 200000)
            throw new InvalidDataException("The sync round exceeds its staging limit.");
        foreach (var change in page.Changes)
        {
            change.Identity.Validate();
            if (change.Identity.Provider != scope.Provider || change.Identity.AccountId != scope.AccountId || change.Identity.CalendarId != scope.CalendarId || string.IsNullOrWhiteSpace(change.Identity.EventId))
                throw new InvalidDataException("A sync page belongs to another account or calendar.");
            change.Observation.Validate();
        }
        // Validate the entire page before changing staging state.
        foreach (var change in page.Changes) changes[change.Identity] = change;
        if (page.NextPageToken is not null) pageTokens.Add(page.NextPageToken);
        started = true; nextPage = page.NextPageToken; completed = page.CompletedCursor;
    }
    public void Commit(Action<SyncCommit> persistAtomically)
    {
        if (completed is null || committed) throw new InvalidOperationException("Complete every page before committing a sync round.");
        persistAtomically(new(scope, changes.Values.ToArray(), completed));
        CommittedCursor = completed; committed = true;
    }
}

public sealed record RemoteWrite(Guid OperationId, RemoteEventIdentity? Identity, CalendarEvent? Event, string? ExpectedVersion)
{
    public void Validate()
    {
        if (OperationId == Guid.Empty || Identity is null && Event is null || Identity is not null && string.IsNullOrWhiteSpace(ExpectedVersion))
            throw new InvalidDataException("Remote writes require an operation identity and existing items require a concurrency token.");
        Event?.Validate();
        Identity?.Validate();
    }
}
public sealed record RemoteWriteResult(RemoteEventIdentity Identity, string? Version, bool Deleted = false);

public interface ICalendarProvider
{
    CalendarProviderKind Kind { get; }
    Task<IReadOnlyList<RemoteCalendar>> ListCalendarsAsync(string accountId, CancellationToken cancellationToken);
    Task<ChangePage> ReadChangesAsync(SyncScope scope, string? cursor, string? pageToken, CancellationToken cancellationToken);
    // OperationId survives retries. Existing writes require an opaque provider concurrency
    // token; adapters must not convert a failed conditional write into an unconditional one.
    Task<RemoteWriteResult> WriteAsync(RemoteWrite operation, CancellationToken cancellationToken);
}
