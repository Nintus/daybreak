using System.IO;
using System.Net;
using System.Net.Http;
using Daymark.Core;
using Daymark.Providers;

namespace Daymark.Windows;

internal sealed class AccountService(string directory)
{
    public AccountAuthorization Authorization { get; } = new(directory);
    private readonly ProviderCache cache = new(Path.Combine(directory, "accounts.sqlite"));
    private readonly HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }) { Timeout = TimeSpan.FromSeconds(60) };
    private readonly Dictionary<Guid, DateTime> cooldowns = [];
    public IReadOnlyList<(ConnectedAccount Account, List<RemoteCalendar> Calendars)> Accounts() => cache.Accounts();
    public IReadOnlyList<CachedCalendar> Read() => cache.Read();
    private CalendarReadClient Client(ConnectedAccount account) => new(http, ct => Authorization.TokenAsync(account, ct));
    public async Task ConnectAsync(CalendarProviderKind provider, CancellationToken ct)
    {
        var account = await Authorization.ConnectAsync(provider, ct);
        try
        {
            var calendars = await Client(account).ListAsync(account, ct);
            if (provider == CalendarProviderKind.Google) account = account with { Label = "Google · " + (Accounts().Count(a => a.Account.Provider == provider) + 1) };
            cache.SaveDiscovery(account, calendars);
        }
        catch { await Authorization.ForgetAsync(account); throw; }
    }
    public async Task RefreshAsync(ConnectedAccount account, string[] selected, CancellationToken ct)
    {
        if (cooldowns.TryGetValue(account.Id, out var until) && until > DateTime.UtcNow)
            throw new InvalidDataException("The provider asked us to wait. Try again after " + until.ToLocalTime().ToString("t") + ".");
        try
        {
            var client = Client(account); var catalog = await client.ListAsync(account, ct);
            if (selected.Any(id => !catalog.Any(c => c.Id == id))) throw new InvalidDataException("A selected calendar is no longer accessible. Disconnect and reconnect to refresh discovery.");
            account = account with { SelectedCalendars = selected };
            if (selected.Length > 50 || selected.Distinct(StringComparer.Ordinal).Count() != selected.Length) throw new InvalidDataException("Select at most 50 distinct calendars.");
            var today = DateTime.UtcNow.Date;
            var first = today.AddMonths(-3); var last = today.AddMonths(15);
            var snapshots = new List<CalendarSnapshot>();
            long bytes = 0;
            foreach (var calendar in catalog.Where(c => selected.Contains(c.Id, StringComparer.Ordinal)))
            {
                var snapshot = await client.ReadAsync(account, calendar, first, last, ct);
                bytes += snapshot.Events.Sum(e => (long)System.Text.Encoding.UTF8.GetByteCount(e.RawJson));
                if (bytes > 64 * 1024 * 1024) throw new InvalidDataException("Selected calendars exceed the 64 MiB account download limit. Select fewer calendars.");
                snapshots.Add(snapshot);
            }
            ct.ThrowIfCancellationRequested();
            cache.Commit(account, catalog, snapshots);
        }
        catch (ProviderRequestException error) when (error.Status == 429 || error.Status >= 500)
        {
            var delay = error.RetryAfter is { } requested && requested > TimeSpan.Zero ? requested : TimeSpan.FromMinutes(1);
            var now = DateTime.UtcNow;
            cooldowns[account.Id] = delay < DateTime.MaxValue - now ? now.Add(delay) : DateTime.MaxValue;
            throw;
        }
    }
    public async Task ForgetAsync(ConnectedAccount account)
    {
        // Stop credentials first; do not leave an active connection after a failed cache removal.
        await Authorization.ForgetAsync(account); cache.Forget(account.Id); cooldowns.Remove(account.Id);
    }
    public static string ErrorMessage(Exception error) => error switch
    {
        OperationCanceledException => "The operation was cancelled or timed out. The previous calendar cache was retained.",
        ProviderRequestException or InvalidDataException => error.Message,
        Microsoft.Identity.Client.MsalException or Google.Apis.Auth.OAuth2.Responses.TokenResponseException => "Sign-in could not be completed. Check the client registration, consent and account access; reconnect if authorization has expired.",
        HttpRequestException => "Could not reach the calendar provider. The previous cache remains available offline.",
        _ => "The account operation could not be completed. The previous cache was retained. Check access to the app's data folder."
    };
}
