using Daymark.Core;

internal static class SyncPlanningTests
{
    public static void Run(Action<string, Action> test, Action<bool> check, Action<Action> reject)
    {
        var item = new CalendarEvent(Guid.NewGuid(), "Planning", new(2026, 11, 3, 9, 0, 0), new(2026, 11, 3, 10, 0, 0));
        var local = item with { Title = "Local edit" }; var remoteEdit = item with { Location = "Remote room" };
        RemoteObservation Seen(CalendarEvent value, string? version = "opaque-v2") => new(value, version);
        var deleted = new RemoteObservation(null, "opaque-v3", true);
        test("unchanged mapped events do not produce network writes", () => check(SyncMerge.Decide(item, item, Seen(item)).Action == SyncAction.None));
        test("remote-only edits update the local value", () => check(SyncMerge.Decide(item, item, Seen(remoteEdit)).Action == SyncAction.UpdateLocal));
        test("local-only edits require the latest opaque remote concurrency token", () =>
        {
            var plan = SyncMerge.Decide(item, local, Seen(item));
            check(plan.Action == SyncAction.UpdateRemote && plan.ExpectedRemoteVersion == "opaque-v2");
            check(SyncMerge.Decide(item, local, Seen(item, null)).Action == SyncAction.Conflict);
        });
        test("simultaneous divergent edits preserve both sides as a conflict", () =>
        {
            var plan = SyncMerge.Decide(item, local, Seen(remoteEdit));
            check(plan.Action == SyncAction.Conflict && plan.Local == local && plan.Remote == remoteEdit);
        });
        test("identical concurrent changes converge without an upload", () => check(SyncMerge.Decide(item, local, Seen(local)).Action == SyncAction.Converged));
        test("remote deletion cannot silently discard a local edit", () =>
        {
            check(SyncMerge.Decide(item, item, deleted).Action == SyncAction.DeleteLocal);
            check(SyncMerge.Decide(item, local, deleted).Action == SyncAction.Conflict);
        });
        test("local deletion cannot silently discard a remote edit", () =>
        {
            var plan = SyncMerge.Decide(item, null, Seen(item));
            check(plan.Action == SyncAction.DeleteRemote && plan.ExpectedRemoteVersion == "opaque-v2");
            check(SyncMerge.Decide(item, null, Seen(remoteEdit)).Action == SyncAction.Conflict);
            check(SyncMerge.Decide(item, null, deleted).Action == SyncAction.Converged);
        });
        test("new local and remote items have separate create and import paths", () =>
        {
            check(SyncMerge.Decide(null, null, Seen(item)).Action == SyncAction.ImportRemote);
            check(SyncMerge.Decide(null, item, deleted).Action == SyncAction.CreateRemote);
            check(SyncMerge.Decide(null, null, deleted).Action == SyncAction.None);
        });
        test("different unmapped identities are never matched by title or time", () =>
            reject(() => SyncMerge.Decide(item, item, Seen(item with { Id = Guid.NewGuid() }))));
        test("absent delta entries cannot be mistaken for deletions", () =>
        {
            reject(() => new RemoteObservation(null, null).Validate());
            reject(() => new RemoteObservation(item, "v1", true).Validate());
        });
        test("authentication permission and expired cursor failures have distinct recovery paths", () =>
        {
            check(SyncFailures.Classify(401, 0).Action == SyncFailureAction.SignInRequired);
            check(SyncFailures.Classify(403, 0).Action == SyncFailureAction.PermissionRequired);
            check(SyncFailures.Classify(410, 0).Action == SyncFailureAction.FullResync);
        });
        test("concurrency and missing-item errors require reconciliation rather than blind retry", () =>
        {
            foreach (var status in new[] { 404, 409, 412 }) check(SyncFailures.Classify(status, 0).Action == SyncFailureAction.Reconcile);
        });
        test("rate limits honor Retry-After and retries have a finite budget", () =>
        {
            check(SyncFailures.Classify(429, 0, TimeSpan.FromHours(1)).Delay == TimeSpan.FromHours(1));
            check(SyncFailures.Classify(503, 2).Delay == TimeSpan.FromSeconds(8));
            check(SyncFailures.Classify(503, 6).Action == SyncFailureAction.Stop);
            check(SyncFailures.Classify(400, 0).Action == SyncFailureAction.Stop);
            check(SyncFailures.Classify(403, 0, reason: "userRateLimitExceeded").Action == SyncFailureAction.RetryLater);
        });
        var scope = new SyncScope(CalendarProviderKind.Google, "account-a", "calendar-a");
        var identity = new RemoteEventIdentity(scope.Provider, scope.AccountId, scope.CalendarId, "remote-event-a");
        var change = new RemoteChange(identity, Seen(item));
        test("partial pagination never advances the committed checkpoint", () =>
        {
            var batch = new SyncPageBatch(scope, "old"); batch.Stage(new([change], "page-2", null));
            check(batch.CommittedCursor == "old" && batch.NextPageToken == "page-2");
            try { batch.Commit(_ => throw new Exception("Should not commit")); throw new Exception("Expected incomplete batch rejection"); }
            catch (InvalidOperationException) { }
        });
        test("all pages and the completed cursor are committed together", () =>
        {
            var batch = new SyncPageBatch(scope, "old"); batch.Stage(new([change], "page-2", null));
            batch.Stage(new([change with { Observation = Seen(remoteEdit, "v3") }], null, "new"), "page-2");
            check(batch.CommittedCursor == "old");
            batch.Commit(commit => check(commit.Changes.Single().Observation.Event == remoteEdit && commit.CompletedCursor == "new" && commit.Scope == scope));
            check(batch.CommittedCursor == "new");
        });
        test("failed persistence preserves the old cursor and permits a complete retry", () =>
        {
            var batch = new SyncPageBatch(scope, "old"); batch.Stage(new([change], null, "new"));
            try { batch.Commit(_ => throw new IOException("Disk full")); } catch (IOException) { }
            check(batch.CommittedCursor == "old"); batch.Commit(_ => { }); check(batch.CommittedCursor == "new");
        });
        test("sync pages cannot cross account or calendar boundaries", () =>
        {
            var batch = new SyncPageBatch(scope, "old");
            reject(() => batch.Stage(new([change with { Identity = identity with { AccountId = "account-b" } }], null, "new")));
            check(batch.CommittedCursor == "old"); batch.Stage(new([change], null, "new"));
        });
        test("pagination rejects wrong tokens cycles and ambiguous completion", () =>
        {
            var batch = new SyncPageBatch(scope, "old");
            reject(() => batch.Stage(new([], "page-2", "new")));
            reject(() => batch.Stage(new([], null, null)));
            batch.Stage(new([], "a", null));
            reject(() => batch.Stage(new([], "b", null), "wrong"));
            batch.Stage(new([], "b", null), "a");
            reject(() => batch.Stage(new([], "a", null), "b"));
        });
        test("Microsoft view checkpoints require an explicit UTC range", () =>
        {
            reject(() => new SyncScope(CalendarProviderKind.Microsoft, "account", "calendar").Validate());
            new SyncScope(CalendarProviderKind.Microsoft, "account", "calendar", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc)).Validate();
        });
        test("writes carry a stable operation ID and existing writes require conditional versions", () =>
        {
            var operation = new RemoteWrite(Guid.NewGuid(), identity, local, "opaque-v2"); operation.Validate();
            reject(() => (operation with { OperationId = Guid.Empty }).Validate());
            reject(() => (operation with { ExpectedVersion = null }).Validate());
            reject(() => new RemoteWrite(Guid.NewGuid(), null, null, null).Validate());
        });
    }
}
