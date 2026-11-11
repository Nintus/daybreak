using System.Security.Cryptography;
using System.Text;
using Daymark.Core;

namespace Daymark.Providers;

public sealed record ConnectedAccount(Guid Id, CalendarProviderKind Provider, string Label, string ClientId,
    string? Subject = null, string[]? SelectedCalendars = null)
{
    public string[] Selection => SelectedCalendars ?? [];
}
public sealed record CachedEvent(string RemoteId, CalendarEvent Event, string RawJson);
public sealed record CalendarSnapshot(ConnectedAccount Account, RemoteCalendar Calendar, DateTime StartUtc,
    DateTime EndUtc, List<CachedEvent> Events, DateTime RetrievedUtc);
public sealed record CachedCalendar(CalendarDefinition Calendar, List<CalendarEvent> Events, DateTime StartUtc,
    DateTime EndUtc, DateTime RetrievedUtc);
public sealed record CachedCalendarCoverage(Guid AccountId, string CalendarId, DateTime StartUtc, DateTime EndUtc,
    DateTime RetrievedUtc, int EventCount);

public static class ProviderIdentity
{
    // Length-prefix fields so opaque IDs containing separators cannot collide.
    public static Guid Event(ConnectedAccount account, string calendar, string remote) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"{account.Id:N}:{(int)account.Provider}:{calendar.Length}:{calendar}{remote.Length}:{remote}"))[..16]);
    public static string Calendar(ConnectedAccount account, string calendar) => "remote-" + Event(account, calendar, "").ToString("N");
}

public sealed class ProviderRequestException(int status, TimeSpan? retryAfter = null)
    : Exception(status switch
    {
        401 => "Sign in again to refresh this account.",
        403 => "The provider denied calendar access. Check consent and account permissions.",
        429 => "The provider is limiting requests. Wait before refreshing again.",
        _ => $"The calendar provider returned HTTP {status}."
    })
{
    public int Status { get; } = status;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
