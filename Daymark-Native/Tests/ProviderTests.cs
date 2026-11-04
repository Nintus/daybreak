using System.Net;
using System.Text.Json;
using Daymark.Core;
using Daymark.Providers;
using Microsoft.Data.Sqlite;

internal static class ProviderTests
{
    private const string GoogleEvent = """{"id":"instance_1","summary":"Planning","start":{"dateTime":"2026-11-03T09:00:00-06:00","timeZone":"America/Chicago"},"end":{"dateTime":"2026-11-03T10:00:00-06:00"},"recurringEventId":"parent","originalStartTime":{"dateTime":"2026-11-02T09:00:00-06:00"},"unknown":{"preserve":true}}""";
    public static void Run(Action<string, Action> test, Action<bool> check, Action<Action> reject, string folder)
    {
        var account = new ConnectedAccount(Guid.NewGuid(), CalendarProviderKind.Google, "QA Google", "fixture-client", SelectedCalendars: ["c1"]);
        var calendar = new RemoteCalendar("c1", "Work", true);
        CachedEvent Map(string json, ConnectedAccount? connection = null) { using var doc = JsonDocument.Parse(json); return CalendarReadClient.Map(connection ?? account, "c1", doc.RootElement); }
        var mapped = Map(GoogleEvent); var start = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc); var end = start.AddMonths(1);
        CalendarSnapshot Snapshot(string title = "Planning") => new(account, calendar, start, end, [mapped with { Event = mapped.Event with { Title = title } }], DateTime.UtcNow);
        test("Google mapping preserves source zone, UTC instant and unknown raw fields", () =>
        {
            check(mapped.Event.StartUtc == new DateTime(2026, 11, 3, 15, 0, 0, DateTimeKind.Utc));
            check(mapped.Event.Start.Hour == 9 && mapped.Event.TimeZoneId == "America/Chicago" && mapped.RawJson.Contains("unknown"));
            check(mapped.Event.SeriesId is null && mapped.Event.Recurrence is null); // A read-only provider instance, not a local series.
        });
        test("Google all-day dates retain exclusive ends", () =>
        {
            var item = Map("""{"id":"all","summary":"Trip","start":{"date":"2026-11-03"},"end":{"date":"2026-11-05"}}""").Event;
            check(item.AllDay && item.Start == new DateTime(2026, 11, 3) && item.End == new DateTime(2026, 11, 5) && item.StartUtc is null);
        });
        test("Microsoft UTC responses retain original timed zone and immutable identity", () =>
        {
            var microsoft = account with { Provider = CalendarProviderKind.Microsoft };
            var item = Map("""{"id":"immutable-id","subject":"Meeting","start":{"dateTime":"2026-11-03T15:00:00.0000000","timeZone":"UTC"},"end":{"dateTime":"2026-11-03T16:00:00.0000000","timeZone":"UTC"},"originalStartTimeZone":"Central Standard Time","bodyPreview":"Plain text","body":{"contentType":"html","content":"<b>Keep raw</b>"}}""", microsoft).Event;
            check(item.Start.Hour == 9 && item.StartUtc!.Value.Hour == 15 && item.Notes == "Plain text");
        });
        test("Microsoft all-day UTC projection recovers original-zone dates", () =>
        {
            var item = Map("""{"id":"all","subject":"Holiday","isAllDay":true,"start":{"dateTime":"2026-11-03T06:00:00","timeZone":"UTC"},"end":{"dateTime":"2026-11-04T06:00:00","timeZone":"UTC"},"originalStartTimeZone":"Central Standard Time"}""", account with { Provider = CalendarProviderKind.Microsoft }).Event;
            check(item.AllDay && item.Start == new DateTime(2026, 11, 3) && item.End == new DateTime(2026, 11, 4));
        });
        test("opaque provider identity includes account and case-sensitive calendar/event IDs", () =>
        {
            check(mapped.Event.Id != Map(GoogleEvent, account with { Id = Guid.NewGuid() }).Event.Id);
            check(ProviderIdentity.Event(account, "ab", "c") != ProviderIdentity.Event(account, "a", "bc"));
            check(ProviderIdentity.Event(account, "C1", "id") != ProviderIdentity.Event(account, "c1", "id"));
        });
        test("Google timed data missing offsets cannot silently use the PC timezone", () => reject(() => Map(GoogleEvent.Replace("-06:00", ""))));
        test("Google transport reads every page with escaped opaque IDs and tokens", () =>
        {
            var calls = 0;
            using var http = new HttpClient(new FixtureHandler(request =>
            {
                check(request.Headers.Authorization?.Parameter == "fixture-token");
                check(request.RequestUri!.AbsoluteUri.Contains("c%2F1")); calls++;
                return calls == 1 ? Json("{\"items\":[" + GoogleEvent + "],\"nextPageToken\":\"opaque / token\"}") : Json("{\"items\":[]}");
            }));
            var snapshot = new CalendarReadClient(http, _ => Task.FromResult("fixture-token")).ReadAsync(account, calendar with { Id = "c/1" }, start, end, default).GetAwaiter().GetResult();
            check(calls == 2 && snapshot.Events.Count == 1);
        });
        test("Graph continuation cannot send tokens to another host or calendar", () =>
        {
            foreach (var link in new[] { "https://evil.example/steal", "https://graph.microsoft.com/v1.0/me/messages" })
            {
                var calls = 0;
                using var http = new HttpClient(new FixtureHandler(request => { calls++; check(request.Headers.GetValues("Prefer").Single().Contains("ImmutableId")); return Json(JsonSerializer.Serialize(new Dictionary<string, object> { ["value"] = Array.Empty<object>(), ["@odata.nextLink"] = link })); }));
                reject(() => new CalendarReadClient(http, _ => Task.FromResult("fixture-token")).ListAsync(account with { Provider = CalendarProviderKind.Microsoft }, default).GetAwaiter().GetResult());
                check(calls == 1);
            }
        });
        test("pagination cycles and failed later pages do not produce a completed snapshot", () =>
        {
            using var cyclic = new HttpClient(new FixtureHandler(_ => Json("{\"items\":[],\"nextPageToken\":\"same\"}")));
            reject(() => new CalendarReadClient(cyclic, _ => Task.FromResult("x")).ReadAsync(account, calendar, start, end, default).GetAwaiter().GetResult());
            var calls = 0;
            using var failed = new HttpClient(new FixtureHandler(_ => ++calls == 1 ? Json("{\"items\":[],\"nextPageToken\":\"page2\"}") : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
            try { new CalendarReadClient(failed, _ => Task.FromResult("x")).ReadAsync(account, calendar, start, end, default).GetAwaiter().GetResult(); throw new Exception("Expected failure"); }
            catch (ProviderRequestException e) { check(e.Status == 503 && calls == 2); }
        });
        test("provider cancellation stops the network query", () =>
        {
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            using var http = new HttpClient(new FixtureHandler(_ => throw new Exception("Should not send")));
            try { new CalendarReadClient(http, _ => Task.FromResult("x")).ListAsync(account, cancelled.Token).GetAwaiter().GetResult(); throw new Exception("Expected cancellation"); }
            catch (OperationCanceledException) { }
        });
        var path = Path.Combine(folder, "provider-cache.sqlite"); var cache = new ProviderCache(path);
        test("snapshot, raw payload, coverage and selection survive cache reopen", () =>
        {
            cache.Commit(account, [calendar], [Snapshot()]);
            var reloaded = new ProviderCache(path); var saved = reloaded.Read().Single();
            check(saved.Events.Single() == mapped.Event && saved.StartUtc == start && saved.EndUtc == end && reloaded.Accounts().Single().Account.Selection.Single() == "c1");
        });
        test("cache replacement removes provider-deleted instances only after complete snapshot", () =>
        {
            reject(() => cache.Commit(account, [calendar], [])); check(cache.Read().Single().Events.Count == 1);
            cache.Commit(account, [calendar], [Snapshot() with { Events = [] }]); check(cache.Read().Single().Events.Count == 0);
            cache.Commit(account, [calendar], [Snapshot()]);
        });
        test("cache rejects events mapped to another account", () =>
        {
            reject(() => cache.Commit(account, [calendar], [Snapshot() with { Events = [Map(GoogleEvent, account with { Id = Guid.NewGuid() })] }]));
            check(cache.Read().Single().Events.Single().Title == "Planning");
        });
        test("failed SQLite refresh rolls back earlier calendar writes and account selection", () =>
        {
            using var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString()); db.Open();
            using var trigger = db.CreateCommand(); trigger.CommandText = "CREATE TRIGGER fixture_failure BEFORE INSERT ON snapshots WHEN NEW.calendar='c2' BEGIN SELECT RAISE(ABORT,'fixture failure'); END"; trigger.ExecuteNonQuery();
            var expanded = account with { SelectedCalendars = ["c1", "c2"] }; var second = calendar with { Id = "c2" };
            try { cache.Commit(expanded, [calendar, second], [Snapshot("New title") with { Account = expanded }, new(expanded, second, start, end, [], DateTime.UtcNow)]); throw new Exception("Expected transaction failure"); }
            catch (SqliteException) { }
            check(cache.Read().Single().Events.Single().Title == "Planning" && cache.Accounts().Single().Account.Selection.Length == 1);
        });
        test("disconnect forgets cache without modifying other accounts", () =>
        {
            var other = account with { Id = Guid.NewGuid(), SelectedCalendars = [] }; cache.SaveDiscovery(other, [calendar]);
            cache.Forget(account.Id); check(cache.Read().Count == 0 && cache.Accounts().Single().Account.Id == other.Id);
        });
        SqliteConnection.ClearAllPools();
    }
    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
    private sealed class FixtureHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.FromResult(respond(request)); }
    }
}
