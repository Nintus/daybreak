using System.Text.Json;
using Daymark.Core;
using Microsoft.Data.Sqlite;

namespace Daymark.Providers;

// Provider cache is deliberately separate from local calendar JSON and undo history.
// All calendars in one refresh, their coverage, and account selection commit together.
public sealed class ProviderCache
{
    private readonly string connectionString;
    public ProviderCache(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true, DefaultTimeout = 5 }.ToString();
        using var db = Open();
        using var version = db.CreateCommand(); version.CommandText = "PRAGMA user_version";
        var current = Convert.ToInt32(version.ExecuteScalar());
        if (current is not (0 or 1)) throw new InvalidDataException("This account cache was created by a newer Daymark version.");
        using var schema = db.CreateCommand();
        schema.CommandText = """
            CREATE TABLE IF NOT EXISTS accounts(id TEXT PRIMARY KEY, data TEXT NOT NULL, catalog TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS snapshots(account TEXT NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
                calendar TEXT NOT NULL, data TEXT NOT NULL, PRIMARY KEY(account,calendar));
            PRAGMA user_version=1;
            """;
        schema.ExecuteNonQuery();
    }
    private SqliteConnection Open() { var db = new SqliteConnection(connectionString); db.Open(); return db; }
    public IReadOnlyList<(ConnectedAccount Account, List<RemoteCalendar> Calendars)> Accounts()
    {
        using var db = Open(); using var command = db.CreateCommand(); command.CommandText = "SELECT data,catalog FROM accounts ORDER BY rowid";
        using var reader = command.ExecuteReader(); var rows = new List<(ConnectedAccount, List<RemoteCalendar>)>();
        while (reader.Read()) rows.Add((JsonSerializer.Deserialize<ConnectedAccount>(reader.GetString(0))!, JsonSerializer.Deserialize<List<RemoteCalendar>>(reader.GetString(1))!));
        return rows;
    }
    public void SaveDiscovery(ConnectedAccount account, IReadOnlyList<RemoteCalendar> calendars)
    {
        Validate(account, calendars);
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "INSERT INTO accounts(id,data,catalog) VALUES($id,$data,$catalog) ON CONFLICT(id) DO UPDATE SET data=$data,catalog=$catalog";
        command.Parameters.AddWithValue("$id", account.Id.ToString("N")); command.Parameters.AddWithValue("$data", JsonSerializer.Serialize(account));
        command.Parameters.AddWithValue("$catalog", JsonSerializer.Serialize(calendars)); command.ExecuteNonQuery();
    }
    public void Commit(ConnectedAccount account, IReadOnlyList<RemoteCalendar> catalog, IReadOnlyList<CalendarSnapshot> snapshots)
    {
        Validate(account, catalog);
        if (!account.Selection.ToHashSet(StringComparer.Ordinal).SetEquals(snapshots.Select(s => s.Calendar.Id)) || snapshots.Count != account.Selection.Length)
            throw new InvalidDataException("Every selected calendar must finish before a refresh is committed.");
        foreach (var snapshot in snapshots)
        {
            if (snapshot.Account.Id != account.Id || snapshot.Account.Provider != account.Provider || snapshot.StartUtc.Kind != DateTimeKind.Utc || snapshot.EndUtc.Kind != DateTimeKind.Utc || snapshot.EndUtc <= snapshot.StartUtc || snapshot.RetrievedUtc.Kind != DateTimeKind.Utc)
                throw new InvalidDataException("Invalid snapshot scope.");
            if (snapshot.Events.Count > 50000 || snapshot.Events.Select(e => e.RemoteId).Distinct(StringComparer.Ordinal).Count() != snapshot.Events.Count)
                throw new InvalidDataException("Invalid snapshot identities.");
            foreach (var entry in snapshot.Events)
            {
                entry.Event.Validate();
                if (entry.Event.Id != ProviderIdentity.Event(account, snapshot.Calendar.Id, entry.RemoteId) || entry.Event.CalendarId != ProviderIdentity.Calendar(account, snapshot.Calendar.Id))
                    throw new InvalidDataException("An event belongs to a different provider scope.");
            }
        }
        var serialized = snapshots.Select(s => (Snapshot: s, Data: JsonSerializer.Serialize(s))).ToArray();
        var bytes = serialized.Sum(s => (long)System.Text.Encoding.UTF8.GetByteCount(s.Data));
        if (bytes > 128 * 1024 * 1024 || snapshots.Sum(s => s.Events.Count) > 100000)
            throw new InvalidDataException("This account exceeds the 128 MiB / 100,000-event cache limit.");
        using var db = Open(); using var transaction = db.BeginTransaction();
        using var save = db.CreateCommand(); save.Transaction = transaction;
        save.CommandText = "INSERT INTO accounts(id,data,catalog) VALUES($id,$data,$catalog) ON CONFLICT(id) DO UPDATE SET data=$data,catalog=$catalog";
        save.Parameters.AddWithValue("$id", account.Id.ToString("N")); save.Parameters.AddWithValue("$data", JsonSerializer.Serialize(account));
        save.Parameters.AddWithValue("$catalog", JsonSerializer.Serialize(catalog)); save.ExecuteNonQuery();
        foreach (var (snapshot, data) in serialized)
        {
            using var command = db.CreateCommand(); command.Transaction = transaction;
            command.CommandText = "INSERT INTO snapshots(account,calendar,data) VALUES($id,$calendar,$data) ON CONFLICT(account,calendar) DO UPDATE SET data=$data";
            command.Parameters.AddWithValue("$id", account.Id.ToString("N")); command.Parameters.AddWithValue("$calendar", snapshot.Calendar.Id);
            command.Parameters.AddWithValue("$data", data); command.ExecuteNonQuery();
        }
        transaction.Commit();
    }
    public IReadOnlyList<CachedCalendar> Read()
    {
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "SELECT a.data,s.data FROM accounts a JOIN snapshots s ON s.account=a.id";
        using var reader = command.ExecuteReader(); var result = new List<CachedCalendar>();
        while (reader.Read())
        {
            var account = JsonSerializer.Deserialize<ConnectedAccount>(reader.GetString(0))!;
            var snapshot = JsonSerializer.Deserialize<CalendarSnapshot>(reader.GetString(1))!;
            if (!account.Selection.Contains(snapshot.Calendar.Id, StringComparer.Ordinal)) continue;
            var name = $"{account.Label} · {snapshot.Calendar.Name}"; if (name.Length > 80) name = name[..80];
            result.Add(new(new(ProviderIdentity.Calendar(account, snapshot.Calendar.Id), name, account.Provider == CalendarProviderKind.Google ? "Teal" : "Violet"),
                snapshot.Events.Select(e => e.Event).ToList(), snapshot.StartUtc, snapshot.EndUtc, snapshot.RetrievedUtc));
        }
        return result;
    }
    public void Forget(Guid accountId)
    {
        using var db = Open(); using var command = db.CreateCommand(); command.CommandText = "DELETE FROM accounts WHERE id=$id";
        command.Parameters.AddWithValue("$id", accountId.ToString("N")); command.ExecuteNonQuery();
    }
    private static void Validate(ConnectedAccount account, IReadOnlyList<RemoteCalendar> catalog)
    {
        if (account.Id == Guid.Empty || !Enum.IsDefined(account.Provider) || string.IsNullOrWhiteSpace(account.ClientId) || string.IsNullOrWhiteSpace(account.Label) || account.Label.Length > 80 || catalog.Count > 1000 ||
            catalog.Any(c => string.IsNullOrWhiteSpace(c.Id)) || catalog.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != catalog.Count || account.Selection.Length > 50 || account.Selection.Distinct(StringComparer.Ordinal).Count() != account.Selection.Length || account.Selection.Any(id => !catalog.Any(c => c.Id == id)))
            throw new InvalidDataException("Invalid connected account or calendar selection (maximum 50 calendars).");
    }
}
