using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Daymark.Core;

namespace Daymark.Migration;

public sealed record AccountDraft(string Id, string Source, string Name, string Login, string Provider, string Server = "");
public sealed record SettingCandidate(string Source, string Theme = "", int? DefaultDurationMinutes = null);
public sealed record DiscoveryReport(List<AccountDraft> Accounts, List<string> CalendarFiles, List<SettingCandidate> Settings, List<string> Notes);
public sealed record CalendarImport(CalendarDefinition Calendar, List<CalendarEvent> Events, List<string> Warnings);

public static class OutlookProfileIdentity
{
    public static AccountDraft? Read(Func<string, object?> value)
    {
        string Text(string key) => value(key) switch
        { string s when s.Length <= 4096 => s, byte[] b when b.Length <= 4096 && b.Length % 2 == 0 => Encoding.Unicode.GetString(b).Trim('\0', '\uFEFF'), _ => "" };
        var login = Text("Email"); if (login.Length == 0) login = Text("Email Address"); if (login.Length == 0) login = Text("SMTP Email Address");
        var server = Text("SMTP Server"); var service = Text("Service Name");
        return ImportIdentity.Account("Outlook", Text("Account Name"), login, server.Contains("://") ? server : "https://" + server,
            service.Equals("MSEMS", StringComparison.OrdinalIgnoreCase) ? "exchange" : "");
    }
}

public static class ImportIdentity
{
    public static Guid Guid(string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes(key))[..16]);
    public static bool IsEmail(string login) => MailAddress.TryCreate(login, out var address) && address.Address == login && login.Length <= 320;
    public static AccountDraft? Account(string source, string name, string login, string server, string type = "")
    {
        login = login.Trim(); if (login.Length == 0 || login.Length > 320 || login.Any(char.IsControl)) return null;
        var host = Uri.TryCreate(server, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" ? uri.IdnHost : "";
        // Retain the server origin only: subscription URLs can embed bearer credentials in paths/queries.
        var origin = host.Length == 0 ? "" : uri!.GetLeftPart(UriPartial.Authority);
        if (uri?.UserInfo.Length > 0) origin = uri.Scheme + "://" + uri.IdnHost + (uri.IsDefaultPort ? "" : ":" + uri.Port);
        var hint = (host + " " + type).ToLowerInvariant();
        var domain = IsEmail(login) ? login[(login.IndexOf('@') + 1)..].ToLowerInvariant() : "";
        bool Host(string suffix) => host.Equals(suffix, StringComparison.OrdinalIgnoreCase) || host.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase);
        var provider = Host("google.com") || Host("googleapis.com") || host.Equals("apidata.googleusercontent.com", StringComparison.OrdinalIgnoreCase) || domain is "gmail.com" or "googlemail.com" ? "Google"
            : Host("outlook.com") || Host("office365.com") || Host("microsoft.com") || host.Equals("outlook.office.com", StringComparison.OrdinalIgnoreCase) || domain is "outlook.com" or "hotmail.com" or "live.com" || hint.Contains("exchange") || hint.Contains("microsoft") || type == "ews" ? "Microsoft"
            : Host("icloud.com") || domain is "icloud.com" or "me.com" or "mac.com" ? "iCloud"
            : hint.Contains("caldav") ? "CalDAV" : "Unknown";
        var identity = (IsEmail(login) ? login.ToLowerInvariant() : login) + "|" + (provider is "Google" or "Microsoft" or "iCloud" ? provider : origin);
        return new(Guid("account:" + identity).ToString("N"), source, name.Length == 0 ? login : name[..Math.Min(name.Length, 160)], login, provider, origin);
    }
    public static List<AccountDraft> Deduplicate(IEnumerable<AccountDraft> items)
    {
        var rows = items.ToList();
        // An email identity without a server may accompany a more specific calendar account.
        rows = rows.Select(a => a.Provider == "Unknown" && IsEmail(a.Login) && rows.Where(b => b.Provider is "Google" or "Microsoft" or "iCloud" && b.Login.Equals(a.Login, StringComparison.OrdinalIgnoreCase)).Select(b => b.Id).Distinct().Count() == 1
            ? rows.First(b => b.Provider is "Google" or "Microsoft" or "iCloud" && b.Login.Equals(a.Login, StringComparison.OrdinalIgnoreCase)) with { Source = a.Source } : a).ToList();
        return rows.GroupBy(a => a.Id, StringComparer.Ordinal).Select(g => g.First() with { Source = string.Join(", ", g.SelectMany(a => a.Source.Split(", ")).Distinct()).Length <= 500 ? string.Join(", ", g.SelectMany(a => a.Source.Split(", ")).Distinct()) : "Multiple apps" }).ToList();
    }
}

public sealed class AccountDraftStore(string path)
{
    public List<AccountDraft> Read()
    {
        if (!File.Exists(path)) return [];
        if (new FileInfo(path).Length > 2_000_000) throw new InvalidDataException("The imported account list is too large.");
        var drafts = JsonSerializer.Deserialize<List<AccountDraft>>(File.ReadAllText(path)) ?? [];
        if (drafts.Count > 200 || drafts.Any(a => a is null || !System.Guid.TryParseExact(a.Id, "N", out _) ||
            a.Source is null || a.Source.Length > 500 || a.Name is null || a.Name.Length > 160 || a.Login is null || a.Login.Length is 0 or > 320 || a.Login.Any(char.IsControl) ||
            a.Provider is not ("Google" or "Microsoft" or "iCloud" or "CalDAV" or "Unknown") || a.Server is null || a.Server.Length > 2000 ||
            a.Server.Length > 0 && (!Uri.TryCreate(a.Server, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http") || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.AbsolutePath != "/")))
            throw new InvalidDataException("The imported account list is invalid. Restore it from a backup or remove imported-accounts.json from the data folder.");
        return drafts;
    }
    public void Merge(IEnumerable<AccountDraft> selected)
    {
        var drafts = ImportIdentity.Deduplicate(Read().Concat(selected));
        if (drafts.Count > 200) throw new InvalidDataException("Keep at most 200 imported account profiles.");
        Write(drafts);
    }
    public void Remove(string id) => Write(Read().Where(a => a.Id != id).ToList());
    private void Write(List<AccountDraft> drafts)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + System.Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(drafts)); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

public static class ThunderbirdPreferences
{
    public static Dictionary<string, string> Parse(string text)
    {
        if (text.Length > 4_000_000) throw new InvalidDataException("Profile preferences exceed 4 MB.");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(text,
            "user_pref\\(\\s*(\"(?:\\\\.|[^\"\\\\])*\")\\s*,\\s*(\"(?:\\\\.|[^\"\\\\])*\"|true|false|-?[0-9]+)\\s*\\)\\s*;", System.Text.RegularExpressions.RegexOptions.NonBacktracking))
        {
            var key = JsonSerializer.Deserialize<string>(match.Groups[1].Value)!;
            // Never evaluate JavaScript or collect arbitrary preferences/credential fields.
            if (!(key.StartsWith("calendar.registry.") && new[] { ".name", ".uri", ".username", ".type", ".color", ".disabled" }.Any(key.EndsWith) ||
                key.StartsWith("mail.identity.") && new[] { ".useremail", ".fullName" }.Any(key.EndsWith) ||
                key.StartsWith("mail.server.") && new[] { ".userName", ".hostname", ".type" }.Any(key.EndsWith) ||
                key is "extensions.activeThemeID" or "calendar.week.start" or "calendar.event.defaultlength")) continue;
            var value = match.Groups[2].Value;
            result[key] = value.StartsWith('"') ? JsonSerializer.Deserialize<string>(value)! : value;
        }
        return result;
    }
    public static void AddTo(DiscoveryReport report, string text, string source)
    {
        var prefs = Parse(text);
        foreach (var group in prefs.Where(p => p.Key.StartsWith("calendar.registry.") || p.Key.StartsWith("mail.server.") || p.Key.StartsWith("mail.identity."))
            .GroupBy(p => p.Key[..p.Key.LastIndexOf('.')]))
        {
            var row = group.ToDictionary(p => p.Key[(p.Key.LastIndexOf('.') + 1)..], p => p.Value);
            string Value(string key) => row.GetValueOrDefault(key, "");
            var login = Value("useremail"); if (login.Length == 0) login = Value("username"); if (login.Length == 0) login = Value("userName");
            var server = Value("uri"); if (server.Length == 0 && Value("hostname").Length > 0) server = "https://" + Value("hostname");
            if (login.Length == 0 && Uri.TryCreate(server, UriKind.Absolute, out var uri) && uri.UserInfo.Length > 0 && !uri.UserInfo.Contains(':')) login = Uri.UnescapeDataString(uri.UserInfo);
            var name = Value("name"); if (name.Length == 0) name = Value("fullName");
            if (ImportIdentity.Account(source, name, login, server, Value("type")) is { } account) report.Accounts.Add(account);
            if (Uri.TryCreate(server, UriKind.Absolute, out var file) && file.IsFile && Path.GetExtension(file.LocalPath).Equals(".ics", StringComparison.OrdinalIgnoreCase)) report.CalendarFiles.Add(file.LocalPath);
        }
        if (prefs.TryGetValue("extensions.activeThemeID", out var theme) && theme is "thunderbird-dark@mozilla.org" or "thunderbird-light@mozilla.org") report.Settings.Add(new(source, theme.Contains("dark") ? "Dark" : "Light"));
        if (prefs.TryGetValue("calendar.event.defaultlength", out var length) && int.TryParse(length, out var minutes) && minutes is >= 1 and <= 1440) report.Settings.Add(new(source, DefaultDurationMinutes: minutes));
        if (prefs.ContainsKey("calendar.week.start")) report.Notes.Add(source + ": week-start preference detected; Daymark currently uses Monday, so it is not changed.");
    }
}

public static class FantasticalPreferences
{
    public static void AddTo(DiscoveryReport report, string text)
    {
        if (text.Length > 4_000_000) throw new InvalidDataException("Preferences exceed 4 MB.");
        using var reader = System.Xml.XmlReader.Create(new StringReader(text), new System.Xml.XmlReaderSettings
        { DtdProcessing = System.Xml.DtdProcessing.Ignore, XmlResolver = null, MaxCharactersInDocument = 4_000_000 });
        var document = System.Xml.Linq.XDocument.Load(reader);
        var dict = document.Root?.Element("dict"); if (dict is null) return;
        string Value(string key) => dict.Elements("key").FirstOrDefault(k => k.Value == key)?.ElementsAfterSelf().FirstOrDefault()?.Value ?? "";
        if (int.TryParse(Value("DefaultEventDuration"), out var seconds) && seconds is >= 60 and <= 86400 && seconds % 60 == 0)
            report.Settings.Add(new("Fantastical", DefaultDurationMinutes: seconds / 60));
        if (Value("LightThemeFile") == Value("DarkThemeFile"))
        {
            if (Value("DarkThemeFile") == "dark.theme") report.Settings.Add(new("Fantastical", "Dark"));
            else if (Value("DarkThemeFile") == "light.theme") report.Settings.Add(new("Fantastical", "Light"));
        }
        if (Value("FirstWeekday").Length > 0) report.Notes.Add("Fantastical: week-start preference detected; Daymark currently uses Monday, so it is not changed.");
    }
}
