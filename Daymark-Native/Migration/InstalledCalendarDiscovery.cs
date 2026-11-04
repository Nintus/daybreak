using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Daymark.Migration;

public static class InstalledCalendarDiscovery
{
    public static DiscoveryReport Scan(string roaming, string local, bool includeOutlook = true)
    {
        var report = new DiscoveryReport([], [], [], []);
        var directoriesVisited = 0;
        void Attempt(string source, Action action)
        {
            try { action(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException or System.Security.SecurityException or System.Xml.XmlException)
            { report.Notes.Add(source + ": metadata could not be read. Close the app and try again, or export calendars as .ics."); }
        }
        var fantastical = Path.Combine(roaming, "Fantastical");
        if (IsLocal(fantastical) && Directory.Exists(fantastical)) Attempt("Fantastical", () =>
        {
            var database = Path.Combine(fantastical, "Database");
            if (IsLocal(database) && Directory.Exists(database))
            {
                var sources = Directory.EnumerateFiles(database, "Sources-*.plist").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
                if (sources is not null) Attempt("Fantastical accounts", () => report.Accounts.AddRange(BinaryPlist.Accounts(Read(sources, 4_000_000), "Fantastical")));
            }
            var prefs = Path.Combine(fantastical, "gnustep", "Fantastical.plist");
            if (File.Exists(prefs)) Attempt("Fantastical settings", () => FantasticalPreferences.AddTo(report, Encoding.UTF8.GetString(Read(prefs, 4_000_000))));
            FindCalendars(fantastical, report, 0, ref directoriesVisited);
            report.Notes.Add("Fantastical: account metadata, local .ics files, default event duration and an explicit standard light/dark theme can be imported. Its private calendar database and custom themes are not converted. Sign back in to retrieve server calendars; export local calendars as .ics.");
        });
        var thunderbird = Path.Combine(roaming, "Thunderbird");
        if (IsLocal(thunderbird) && Directory.Exists(thunderbird)) Attempt("Thunderbird", () =>
        {
            var profiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ini = Path.Combine(thunderbird, "profiles.ini");
            if (File.Exists(ini))
            {
                foreach (var section in Regex.Split(Encoding.UTF8.GetString(Read(ini, 200_000)), @"(?m)^\["))
                {
                    if (!section.StartsWith("Profile", StringComparison.Ordinal)) continue;
                    var path = Regex.Match(section, @"(?m)^Path=(.+?)\r?$").Groups[1].Value;
                    if (path.Length == 0) continue;
                    var relative = Regex.IsMatch(section, @"(?m)^IsRelative=1\r?$");
                    var resolved = Path.GetFullPath(relative ? Path.Combine(thunderbird, path) : path);
                    if (IsLocal(resolved)) profiles.Add(resolved);
                }
            }
            // Also handle ordinary profiles when profiles.ini is missing or stale.
            var defaults = Path.Combine(thunderbird, "Profiles");
            if (IsLocal(defaults) && Directory.Exists(defaults)) foreach (var path in Directory.EnumerateDirectories(defaults).Take(50)) if (IsLocal(path)) profiles.Add(path);
            foreach (var profile in profiles.Take(50)) Attempt("Thunderbird profile", () =>
            {
                var prefs = Path.Combine(profile, "prefs.js");
                if (File.Exists(prefs)) ThunderbirdPreferences.AddTo(report, Encoding.UTF8.GetString(Read(prefs, 4_000_000)), "Thunderbird");
                FindCalendars(profile, report, 0, ref directoriesVisited);
            });
            report.Notes.Add("Thunderbird: calendar/mail identities, local .ics files, standard light/dark theme and default event duration are supported. Calendars in local.sqlite must be exported through Thunderbird (Events and Tasks → Export). Mail identities do not prove that a calendar exists on that account.");
        });
        if (includeOutlook && OperatingSystem.IsWindows()) Attempt("Outlook", () => ReadOutlook(report));
        if (IsLocal(Path.Combine(local, "Microsoft", "Outlook")) && Directory.Exists(Path.Combine(local, "Microsoft", "Outlook")) ||
            IsLocal(Path.Combine(local, "Packages")) && Directory.Exists(Path.Combine(local, "Packages")) && Directory.EnumerateDirectories(Path.Combine(local, "Packages"), "Microsoft.OutlookForWindows_*").Any())
            report.Notes.Add("Outlook: classic profile identities are supported when available. New Outlook's private account cache and PST/OST files are not read. Export a calendar as .ics or add its account to Daymark.");
        if (directoriesVisited >= 1000 || report.CalendarFiles.Count >= 100) report.Notes.Add("Automatic file discovery reached its limit (1,000 app folders / 100 files / 3 levels). Add other .ics exports with Add .ics files.");
        if (report.Accounts.Count > 1000) report.Notes.Add("Account discovery reached its 1,000-identity limit. Import profiles in smaller batches.");
        var accounts = ImportIdentity.Deduplicate(report.Accounts.Take(1000));
        if (accounts.Count > 200) report.Notes.Add("Only the first 200 account profiles are shown. Daymark stores at most 200 imported profiles.");
        return report with { Accounts = accounts.Take(200).ToList(), CalendarFiles = report.CalendarFiles.Where(IsLocal).Distinct(StringComparer.OrdinalIgnoreCase).Take(100).ToList() };
    }
    internal static bool IsLocal(string path)
    {
        if (!Path.IsPathFullyQualified(path) || path.StartsWith("\\\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal)) return false;
        try
        {
            var full = Path.GetFullPath(path); var root = Path.GetPathRoot(full)!;
            if (OperatingSystem.IsWindows() && new DriveInfo(root).DriveType == DriveType.Network) return false;
            // Check parents before descendants: a local-looking path can enter a
            // junction to a remote share, even when the final file is not a link.
            var current = root;
            foreach (var part in full[root.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, part);
                try { if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return false; }
                catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException) { return true; }
            }
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return false; }
    }
    private static byte[] Read(string path, int maximum)
    {
        if (!IsLocal(path)) throw new IOException("Automatic discovery skips linked and network locations.");
        using var stream = File.OpenRead(path);
        if (stream.Length > maximum) throw new InvalidDataException("Metadata exceeds its size limit.");
        var bytes = new byte[checked((int)stream.Length)]; stream.ReadExactly(bytes); return bytes;
    }
    private static void FindCalendars(string directory, DiscoveryReport report, int depth, ref int visited)
    {
        if (depth > 3 || visited >= 1000 || report.CalendarFiles.Count >= 100 || !IsLocal(directory)) return;
        visited++;
        foreach (var path in Directory.EnumerateFiles(directory, "*.ics").Take(100 - report.CalendarFiles.Count)) if (IsLocal(path)) report.CalendarFiles.Add(path);
        foreach (var path in Directory.EnumerateDirectories(directory).Take(100))
        {
            if (new[] { "cache", "credentials", "imapmail", "mail", "crashes", "minidumps" }.Any(n => Path.GetFileName(path).Contains(n, StringComparison.OrdinalIgnoreCase))) continue;
            FindCalendars(path, report, depth + 1, ref visited);
        }
    }
    [SupportedOSPlatform("windows")]
    private static void ReadOutlook(DiscoveryReport report)
    {
        foreach (var version in new[] { "16.0", "15.0" })
        {
            using var profiles = Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Office\{version}\Outlook\Profiles", false);
            if (profiles is null) continue;
            foreach (var profile in profiles.GetSubKeyNames().Take(50))
            {
                using var accounts = profiles.OpenSubKey(profile + @"\9375CFF0413111d3B88A00104B2A6676", false);
                if (accounts is null) continue;
                foreach (var key in accounts.GetSubKeyNames().Take(200))
                {
                    using var row = accounts.OpenSubKey(key, false); if (row is null) continue;
                    if (OutlookProfileIdentity.Read(name => row.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames)) is { } account) report.Accounts.Add(account);
                }
            }
            report.Notes.Add("Outlook classic: email identities were read from profile metadata. Organization accounts with custom domains may need the provider chosen during sign-in.");
        }
    }
}
