using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Daymark.Core;

namespace Daymark.Providers;

// First transport milestone: complete, bounded, read-only snapshots. Expanded provider
// instances remain read-only projections; they are never persisted as local series.
public sealed class CalendarReadClient(HttpClient http, Func<CancellationToken, Task<string>> token)
{
    public string? PrimaryCalendarId { get; private set; }
    public async Task<IReadOnlyList<RemoteCalendar>> ListAsync(ConnectedAccount account, CancellationToken ct)
    {
        PrimaryCalendarId = null;
        var google = account.Provider == CalendarProviderKind.Google;
        var first = google ? "https://www.googleapis.com/calendar/v3/users/me/calendarList?maxResults=250"
            : "https://graph.microsoft.com/v1.0/me/calendars?$top=100";
        var result = new List<RemoteCalendar>();
        await Pages(first, google, async root =>
        {
            foreach (var item in Items(root, google))
            {
                if (Flag(item, "deleted") || Flag(item, "hidden")) continue;
                var id = Required(item, "id");
                if (google && Flag(item, "primary")) PrimaryCalendarId = id;
                result.Add(new(id, Text(item, google ? "summaryOverride" : "name") ?? Text(item, "summary") ?? "Calendar",
                    google ? Text(item, "accessRole") is "owner" or "writer" : Flag(item, "canEdit"), Text(item, "backgroundColor")));
            }
            if (result.Count > 1000) throw new InvalidDataException("This account exceeds the calendar discovery limit.");
            await Task.CompletedTask;
        }, ct);
        if (result.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != result.Count)
            throw new InvalidDataException("The provider returned duplicate calendar identifiers.");
        return result;
    }

    public async Task<CalendarSnapshot> ReadAsync(ConnectedAccount account, RemoteCalendar calendar,
        DateTime startUtc, DateTime endUtc, CancellationToken ct)
    {
        if (startUtc.Kind != DateTimeKind.Utc || endUtc.Kind != DateTimeKind.Utc || endUtc <= startUtc || endUtc - startUtc > TimeSpan.FromDays(800))
            throw new InvalidDataException("Choose a UTC calendar coverage window of at most 800 days.");
        var google = account.Provider == CalendarProviderKind.Google;
        var id = Uri.EscapeDataString(calendar.Id);
        var start = Uri.EscapeDataString(startUtc.ToString("O")); var end = Uri.EscapeDataString(endUtc.ToString("O"));
        var first = google ? $"https://www.googleapis.com/calendar/v3/calendars/{id}/events?singleEvents=true&showDeleted=false&maxResults=2500&timeMin={start}&timeMax={end}"
            : $"https://graph.microsoft.com/v1.0/me/calendars/{id}/calendarView?startDateTime={start}&endDateTime={end}&$top=1000";
        var events = new Dictionary<string, CachedEvent>(StringComparer.Ordinal);
        await Pages(first, google, async root =>
        {
            foreach (var item in Items(root, google))
            {
                if (Text(item, "status") == "cancelled" || Flag(item, "isCancelled")) continue;
                var mapped = Map(account, calendar.Id, item);
                events[mapped.RemoteId] = mapped;
                if (events.Count > 50000) throw new InvalidDataException("This calendar exceeds the 50,000-instance coverage limit.");
            }
            await Task.CompletedTask;
        }, ct);
        return new(account, calendar, startUtc, endUtc, events.Values.ToList(), DateTime.UtcNow);
    }

    private async Task Pages(string first, bool google, Func<JsonElement, Task> consume, CancellationToken ct)
    {
        var next = first; var seen = new HashSet<string>(StringComparer.Ordinal); long totalBytes = 0;
        for (var page = 0; next is not null; page++)
        {
            if (page >= 1000 || !seen.Add(next)) throw new InvalidDataException("Provider pagination did not complete within its limit.");
            // Never send a bearer token to a host/path supplied by an untrusted nextLink.
            var uri = new Uri(next);
            var original = new Uri(first);
            if (uri.Scheme != "https" || uri.Host != original.Host || uri.Port != 443 || uri.AbsolutePath != original.AbsolutePath || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0)
                throw new InvalidDataException("The provider returned an invalid continuation URL.");
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await token(ct));
            if (!google) request.Headers.TryAddWithoutValidation("Prefer", "IdType=\"ImmutableId\", outlook.timezone=\"UTC\"");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode) throw new ProviderRequestException((int)response.StatusCode,
                response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow));
            // Bound decompressed response bytes as well as page count.
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var bytes = new MemoryStream(); var buffer = new byte[16384]; int read;
            while ((read = await stream.ReadAsync(buffer, ct)) != 0)
            {
                if (bytes.Length + read > 20 * 1024 * 1024) throw new InvalidDataException("A provider page exceeds 20 MiB.");
                totalBytes += read;
                if (totalBytes > 64 * 1024 * 1024) throw new InvalidDataException("This provider query exceeds 64 MiB.");
                bytes.Write(buffer, 0, read);
            }
            using var json = JsonDocument.Parse(bytes.ToArray());
            await consume(json.RootElement);
            var continuation = Text(json.RootElement, google ? "nextPageToken" : "@odata.nextLink");
            next = string.IsNullOrWhiteSpace(continuation) ? null : google ? first + "&pageToken=" + Uri.EscapeDataString(continuation) : continuation;
        }
    }

    public static CachedEvent Map(ConnectedAccount account, string calendarId, JsonElement item)
    {
        var google = account.Provider == CalendarProviderKind.Google;
        var id = Required(item, "id"); var start = item.GetProperty("start"); var end = item.GetProperty("end");
        var allDay = google ? Text(start, "date") is not null : Flag(item, "isAllDay");
        DateTime from, to; DateTime? fromUtc = null, toUtc = null; string? zoneId = null;
        if (google && allDay)
        {
            from = DateTime.ParseExact(Required(start, "date"), "yyyy-MM-dd", CultureInfo.InvariantCulture);
            to = DateTime.ParseExact(Required(end, "date"), "yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        else
        {
            DateTime Instant(JsonElement point)
            {
                var text = Required(point, "dateTime");
                var hasOffset = System.Text.RegularExpressions.Regex.IsMatch(text, @"(Z|[+-]\d{2}:\d{2})$");
                if (hasOffset) return DateTimeOffset.Parse(text, CultureInfo.InvariantCulture).UtcDateTime;
                if (google)
                {
                    throw new InvalidDataException("A Google event is missing its UTC offset.");
                }
                var clock = DateTime.SpecifyKind(DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), DateTimeKind.Unspecified);
                var zone = CalendarTime.FindZone(Required(point, "timeZone"));
                if (zone.IsInvalidTime(clock) || zone.IsAmbiguousTime(clock)) throw new InvalidDataException("A provider clock time has no unique UTC instant.");
                return TimeZoneInfo.ConvertTimeToUtc(clock, zone);
            }
            fromUtc = Instant(start); toUtc = Instant(end);
            zoneId = (google ? Text(start, "timeZone") : Text(item, "originalStartTimeZone")) ?? "UTC";
            TimeZoneInfo zone;
            try { zone = CalendarTime.FindZone(zoneId); }
            catch (InvalidDataException) { if (allDay) throw; zoneId = "UTC"; zone = TimeZoneInfo.Utc; }
            from = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(fromUtc.Value, zone), DateTimeKind.Unspecified);
            to = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(toUtc.Value, zone), DateTimeKind.Unspecified);
            if (allDay) { zoneId = null; fromUtc = toUtc = null; }
        }
        var title = Text(item, google ? "summary" : "subject");
        var location = google ? Text(item, "location") : item.TryGetProperty("location", out var place) ? Text(place, "displayName") : null;
        // Never render provider HTML; keep the complete payload separately for later mapping.
        var notes = google ? Text(item, "description") : Text(item, "bodyPreview");
        var normalized = new CalendarEvent(ProviderIdentity.Event(account, calendarId, id), Clip(string.IsNullOrWhiteSpace(title) ? "(Untitled)" : title, 300),
            DateTime.SpecifyKind(from, DateTimeKind.Unspecified), DateTime.SpecifyKind(to, DateTimeKind.Unspecified), allDay,
            Clip(location ?? "", 1000), Clip(notes ?? "", 10000), zoneId, fromUtc, toUtc, ProviderIdentity.Calendar(account, calendarId));
        normalized.Validate();
        return new(id, normalized, item.GetRawText());
    }
    private static string Clip(string s, int max) => s.Length <= max ? s : s[..max];
    private static IEnumerable<JsonElement> Items(JsonElement root, bool google) => google && !root.TryGetProperty("items", out _) ? [] : root.GetProperty(google ? "items" : "value").EnumerateArray();
    private static string Required(JsonElement item, string property) => Text(item, property) is { Length: > 0 } value ? value : throw new InvalidDataException($"Provider data is missing {property}.");
    private static string? Text(JsonElement item, string property) => item.ValueKind == JsonValueKind.Object && item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static bool Flag(JsonElement item, string property) => item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;
}
