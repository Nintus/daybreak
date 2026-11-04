using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Daymark.Core;
using Daymark.Providers;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Util.Store;
using Microsoft.Identity.Client;

namespace Daymark.Windows;

internal sealed record OAuthApplications(string GoogleClientId = "", string GoogleClientSecret = "", string MicrosoftClientId = "");

internal sealed class ProtectedAccountFiles(string directory) : IDataStore
{
    private string PathFor(string key) => Path.Combine(directory, Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key))) + ".bin");
    public byte[]? Read(string key) => File.Exists(PathFor(key)) ? ProtectedData.Unprotect(File.ReadAllBytes(PathFor(key)), null, DataProtectionScope.CurrentUser) : null;
    public void Write(string key, byte[] bytes)
    {
        Directory.CreateDirectory(directory);
        var path = PathFor(key); var temporary = path + ".tmp";
        File.WriteAllBytes(temporary, ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser));
        File.Move(temporary, path, true);
    }
    public void Delete(string key) => File.Delete(PathFor(key));
    public Task StoreAsync<T>(string key, T value) { Write(typeof(T).FullName + key, JsonSerializer.SerializeToUtf8Bytes(value)); return Task.CompletedTask; }
    public Task DeleteAsync<T>(string key) { Delete(typeof(T).FullName + key); return Task.CompletedTask; }
    public Task<T> GetAsync<T>(string key) { var data = Read(typeof(T).FullName + key); return Task.FromResult(data is null ? default! : JsonSerializer.Deserialize<T>(data)!); }
    public Task ClearAsync() => throw new NotSupportedException("Clear individual account credentials instead.");
}

internal sealed class AccountAuthorization(string directory)
{
    private readonly ProtectedAccountFiles protectedFiles = new(Path.Combine(directory, "credentials"));
    private readonly Dictionary<Guid, IPublicClientApplication> microsoftApps = [];
    private static readonly string[] MicrosoftScopes = ["https://graph.microsoft.com/Calendars.Read"];
    private static readonly string[] GoogleScopes = ["https://www.googleapis.com/auth/calendar.readonly"];
    public OAuthApplications Applications => protectedFiles.Read("applications") is { } bytes ? JsonSerializer.Deserialize<OAuthApplications>(bytes)! : new();
    public void Configure(OAuthApplications settings)
    {
        if (settings.GoogleClientId.Length > 500 || settings.GoogleClientSecret.Length > 500 || settings.GoogleClientId.Length > 0 && !settings.GoogleClientId.EndsWith(".apps.googleusercontent.com", StringComparison.Ordinal) ||
            settings.MicrosoftClientId.Length > 0 && !Guid.TryParse(settings.MicrosoftClientId, out _))
            throw new InvalidDataException("Enter a Google desktop client ID and/or a Microsoft application (client) GUID.");
        protectedFiles.Write("applications", JsonSerializer.SerializeToUtf8Bytes(settings));
    }
    private GoogleAuthorizationCodeFlow GoogleFlow(ConnectedAccount account)
    {
        var apps = Applications;
        if (apps.GoogleClientId != account.ClientId) throw new InvalidDataException("Restore the Google client settings used by this connection, or disconnect and reconnect it.");
        return new PkceGoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets { ClientId = account.ClientId, ClientSecret = apps.GoogleClientSecret },
            Scopes = GoogleScopes, DataStore = protectedFiles
        });
    }
    private IPublicClientApplication MicrosoftApp(ConnectedAccount account)
    {
        if (microsoftApps.TryGetValue(account.Id, out var app)) return app;
        app = PublicClientApplicationBuilder.Create(account.ClientId).WithAuthority(AzureCloudInstance.AzurePublic, AadAuthorityAudience.AzureAdAndPersonalMicrosoftAccount)
            .WithRedirectUri("http://localhost").Build();
        var key = "msal-" + account.Id.ToString("N");
        app.UserTokenCache.SetBeforeAccess(args => { if (protectedFiles.Read(key) is { } bytes) args.TokenCache.DeserializeMsalV3(bytes); });
        app.UserTokenCache.SetAfterAccess(args => { if (args.HasStateChanged) protectedFiles.Write(key, args.TokenCache.SerializeMsalV3()); });
        microsoftApps[account.Id] = app; return app;
    }
    public async Task<ConnectedAccount> ConnectAsync(CalendarProviderKind provider, CancellationToken ct)
    {
        var apps = Applications;
        var id = provider == CalendarProviderKind.Google ? apps.GoogleClientId : apps.MicrosoftClientId;
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidDataException("Open Account setup first and configure this provider's desktop client ID.");
        var account = new ConnectedAccount(Guid.NewGuid(), provider, provider.ToString(), id);
        try
        {
            if (provider == CalendarProviderKind.Google)
            {
                using var flow = GoogleFlow(account);
                var credential = await new AuthorizationCodeInstalledApp(flow, new LocalServerCodeReceiver()).AuthorizeAsync(account.Id.ToString("N"), ct);
                _ = await credential.GetAccessTokenForRequestAsync(cancellationToken: ct);
                return account;
            }
            var result = await MicrosoftApp(account).AcquireTokenInteractive(MicrosoftScopes).WithUseEmbeddedWebView(false)
                .WithPrompt(Prompt.SelectAccount).ExecuteAsync(ct);
            return account with { Subject = result.Account.HomeAccountId.Identifier, Label = string.IsNullOrWhiteSpace(result.Account.Username) ? "Microsoft" : result.Account.Username[..Math.Min(80, result.Account.Username.Length)] };
        }
        catch { await ForgetAsync(account); throw; }
    }
    public async Task<string> TokenAsync(ConnectedAccount account, CancellationToken ct)
    {
        if (account.Provider == CalendarProviderKind.Google)
        {
            using var flow = GoogleFlow(account);
            var token = await flow.LoadTokenAsync(account.Id.ToString("N"), ct);
            if (token is null) throw new InvalidDataException("Sign-in is required. Disconnect this account, then connect it again.");
            return await new UserCredential(flow, account.Id.ToString("N"), token).GetAccessTokenForRequestAsync(cancellationToken: ct);
        }
        var app = MicrosoftApp(account);
        var cached = (await app.GetAccountsAsync()).SingleOrDefault(a => a.HomeAccountId.Identifier == account.Subject);
        if (cached is null) throw new InvalidDataException("Sign-in is required. Disconnect this account, then connect it again.");
        return (await app.AcquireTokenSilent(MicrosoftScopes, cached).ExecuteAsync(ct)).AccessToken;
    }
    public Task ForgetAsync(ConnectedAccount account)
    {
        protectedFiles.Delete("msal-" + account.Id.ToString("N")); microsoftApps.Remove(account.Id);
        return protectedFiles.DeleteAsync<TokenResponse>(account.Id.ToString("N"));
    }
}
