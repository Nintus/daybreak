using System.IO;
using System.Net;
using System.Net.Http;
using Daymark.Core;
using Daymark.Providers;
using Daymark.Migration;

namespace Daymark.Windows;

internal sealed record AccountOverview(IReadOnlyList<ConnectedCalendarAccount> Connected, IReadOnlyList<AccountDraft> Imported);
internal sealed record ConnectedCalendarAccount(ConnectedAccount Account, IReadOnlyList<RemoteCalendar> Calendars,
    IReadOnlyDictionary<string, CachedCalendarCoverage> Coverage, string? LastIssue, DateTime? RetryUtc);
internal sealed record AccountRefreshProgress(string Message, bool CanCancel = true);

internal sealed class AccountService(string directory)
{
    public AccountAuthorization Authorization { get; } = new(directory);
    public AccountDraftStore ImportedAccounts { get; } = new(Path.Combine(directory, "imported-accounts.json"));
    private readonly ProviderCache cache = new(Path.Combine(directory, "accounts.sqlite"));
    private readonly HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }) { Timeout = TimeSpan.FromSeconds(60) };
    private readonly Dictionary<Guid, DateTime> cooldowns = [];
    private readonly Dictionary<Guid, string> refreshIssues = [];
    public AccountOverview Overview()
    {
        var coverage = cache.ReadCoverage().ToLookup(c => c.AccountId);
        var connected = cache.Accounts().Select(row => new ConnectedCalendarAccount(row.Account, row.Calendars,
            coverage[row.Account.Id].ToDictionary(c => c.CalendarId, StringComparer.Ordinal),
            refreshIssues.GetValueOrDefault(row.Account.Id),
            cooldowns.TryGetValue(row.Account.Id, out var retry) && retry > DateTime.UtcNow ? retry : null)).ToArray();
        return new(connected, ImportedAccounts.Read());
    }
    public IReadOnlyList<CachedCalendar> Read() => cache.Read();
    private CalendarReadClient Client(ConnectedAccount account) => new(http, ct => Authorization.TokenAsync(account, ct));
    public async Task ConnectAsync(CalendarProviderKind provider, CancellationToken ct, AccountDraft? imported = null)
    {
        var account = await Authorization.ConnectAsync(provider, ct, imported is not null && ImportIdentity.IsEmail(imported.Login) ? imported.Login : null);
        try
        {
            var client = Client(account); var calendars = await client.ListAsync(account, ct);
            if (provider == CalendarProviderKind.Google) account = account with { Label = ImportIdentity.IsEmail(client.PrimaryCalendarId ?? "") ? client.PrimaryCalendarId! : "Google · " + (cache.Accounts().Count(a => a.Account.Provider == provider) + 1) };
            ct.ThrowIfCancellationRequested();
            cache.SaveDiscovery(account, calendars);
        }
        catch { await Authorization.ForgetAsync(account); throw; }
        // A login hint is only a suggestion to the provider. Remove the draft only
        // when the signed-in identity matches, otherwise keep it for another attempt.
        if (imported is not null && account.Label.Equals(imported.Login, StringComparison.OrdinalIgnoreCase)) ImportedAccounts.Remove(imported.Id);
    }
    public async Task RefreshAsync(ConnectedAccount account, string[] selected, CancellationToken ct, IProgress<AccountRefreshProgress> progress)
    {
        string? activeCalendar = null;
        try
        {
            if (cooldowns.TryGetValue(account.Id, out var until) && until > DateTime.UtcNow)
                throw new InvalidDataException("The provider asked Daymark to wait before refreshing again.");
            if (selected.Length > 50 || selected.Distinct(StringComparer.Ordinal).Count() != selected.Length)
                throw new InvalidDataException("Select at most 50 distinct calendars.");
            progress.Report(new($"Checking calendars for {account.Label}…"));
            var client = Client(account); var catalog = await client.ListAsync(account, ct);
            if (selected.Any(id => !catalog.Any(c => c.Id == id)))
                throw new InvalidDataException("A selected calendar is no longer accessible. Clear unavailable selections and retry, or reconnect the account to rediscover calendars.");
            account = account with { SelectedCalendars = selected };
            var today = DateTime.UtcNow.Date;
            var first = today.AddMonths(-3); var last = today.AddMonths(15);
            var snapshots = new List<CalendarSnapshot>();
            long bytes = 0;
            foreach (var calendar in catalog.Where(c => selected.Contains(c.Id, StringComparer.Ordinal)))
            {
                activeCalendar = calendar.Name;
                progress.Report(new($"Downloading {snapshots.Count + 1} of {selected.Length} · {calendar.Name}"));
                var snapshot = await client.ReadAsync(account, calendar, first, last, ct);
                activeCalendar = null;
                bytes += snapshot.Events.Sum(e => (long)System.Text.Encoding.UTF8.GetByteCount(e.RawJson));
                if (bytes > 64 * 1024 * 1024) throw new InvalidDataException("Selected calendars exceed the 64 MiB account download limit. Select fewer calendars.");
                snapshots.Add(snapshot);
            }
            ct.ThrowIfCancellationRequested();
            progress.Report(new("Saving downloaded calendars…", CanCancel: false));
            cache.Commit(account, catalog, snapshots);
            refreshIssues.Remove(account.Id);
            cooldowns.Remove(account.Id);
        }
        catch (Exception error)
        {
            if (error is ProviderRequestException request && (request.Status == 429 || request.Status >= 500))
            {
                var delay = request.RetryAfter is { } requested && requested > TimeSpan.Zero ? requested : TimeSpan.FromMinutes(1);
                var now = DateTime.UtcNow;
                // Cap unexpected Retry-After values at the latest representable local date.
                var latest = DateTime.MaxValue.AddDays(-1);
                cooldowns[account.Id] = delay < latest - now ? now.Add(delay) : latest;
            }
            var description = ErrorMessage(error);
            refreshIssues[account.Id] = activeCalendar is null ? description : $"{activeCalendar}\n{description}";
            throw;
        }
    }
    public async Task ForgetAsync(ConnectedAccount account)
    {
        // Stop credentials first; do not leave an active connection after a failed cache removal.
        await Authorization.ForgetAsync(account); cache.Forget(account.Id); cooldowns.Remove(account.Id);
        refreshIssues.Remove(account.Id);
    }
    public static string ErrorMessage(Exception error) => error switch
    {
        OperationCanceledException => "The operation was cancelled or timed out.",
        ProviderRequestException or InvalidDataException => error.Message,
        Microsoft.Identity.Client.MsalException or Google.Apis.Auth.OAuth2.Responses.TokenResponseException => "Sign-in could not be completed. Check the client registration, consent and account access; reconnect if authorization has expired.",
        HttpRequestException => "Could not reach the calendar provider. Check your internet connection and try again.",
        _ => "The account operation could not be completed. Check access to the app's data folder."
    };
}
