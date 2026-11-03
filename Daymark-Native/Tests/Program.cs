using Daymark.Core;

var count = 0;
void Test(string name, Action action) { action(); count++; Console.WriteLine($"PASS {name}"); }
void Check(bool value) { if (!value) throw new Exception("Assertion failed"); }
void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Expected rejection"); }
var folder = Path.Combine(Path.GetTempPath(), "daymark-native-tests-" + Guid.NewGuid());
Directory.CreateDirectory(folder);
try
{
    var item = new CalendarEvent(Guid.NewGuid(), "Test café", new DateTime(2026, 10, 8, 9, 0, 0), new DateTime(2026, 10, 8, 10, 0, 0), Location: "Studio", Notes: "Line one\nLine two");
    var store = new CalendarStore(Path.Combine(folder, "calendar.json"));
    Test("missing file starts empty", () => Check(store.Load().Events.Count == 0));
    Test("round-trip preserves all event fields", () => { store.Save(new(1, [item])); Check(store.Load().Events.Single() == item); });
    var changed = item with { Title = "Changed", End = item.End.AddHours(1) };
    Test("save keeps previous-state backup", () => { store.Save(new(1, [changed])); Check(store.Load().Events.Single() == changed); Check(CalendarStore.Read(store.Path + ".bak").Events.Single() == item); });
    Test("invalid save leaves existing data intact", () => { Reject(() => store.Save(new(1, [changed with { End = changed.Start }]))); Check(store.Load().Events.Single() == changed); });
    Test("duplicate IDs are rejected", () => Reject(() => new CalendarDocument(1, [item, item]).Validate()));
    Test("unknown backup version is rejected", () => Reject(() => new CalendarDocument(2, [item]).Validate()));
    Test("midnight boundary excludes previous day's ending event", () => { var midnight = item with { End = new DateTime(2026, 10, 9) }; Check(new CalendarDocument(1, [midnight]).On(new DateOnly(2026, 10, 9)).Count() == 0); });
    Test("multi-day all-day event uses exclusive end", () => { var days = item with { Start = new DateTime(2026, 10, 8), End = new DateTime(2026, 10, 10), AllDay = true }; days.Validate(); var doc = new CalendarDocument(1, [days]); Check(doc.On(new(2026, 10, 8)).Count() == 1 && doc.On(new(2026, 10, 9)).Count() == 1 && doc.On(new(2026, 10, 10)).Count() == 0); });
    Test("all-day event rejects non-midnight times", () => Reject(() => (item with { AllDay = true }).Validate()));
    Test("UTC values are rejected by local-time model", () => Reject(() => (item with { Start = DateTime.SpecifyKind(item.Start, DateTimeKind.Utc) }).Validate()));
    Test("visible days match individual day queries across boundaries and ordering", () =>
    {
        var first = new DateOnly(2026, 9, 28);
        var events = new List<CalendarEvent>
        {
            item,
            item with { Id = Guid.NewGuid(), Title = "Earlier", Start = item.Start.AddHours(-1) },
            item with { Id = Guid.NewGuid(), Start = new(2026, 9, 1), End = new(2026, 12, 1), AllDay = true },
            item with { Id = Guid.NewGuid(), Start = new(2026, 10, 8), End = new(2026, 10, 9), AllDay = true },
            item with { Id = Guid.NewGuid(), Start = new(2026, 9, 27, 23, 0, 0), End = new(2026, 9, 28) },
            item with { Id = Guid.NewGuid(), Start = new(2026, 11, 8, 23, 0, 0), End = new(2026, 11, 9, 1, 0, 0) }
        };
        var doc = new CalendarDocument(1, events); doc.Validate();
        var days = doc.Days(first, 42);
        Check(days.Count == 42);
        for (var i = 0; i < 42; i++) Check(days[i].SequenceEqual(doc.On(first.AddDays(i))));
    });
    Test("visible days handle empty calendars and reject invalid ranges", () =>
    {
        Check(CalendarDocument.Empty().Days(new(2026, 1, 1), 42).All(day => day.Count == 0));
        foreach (var count in new[] { 0, -1, 367 })
        {
            try { CalendarDocument.Empty().Days(new(2026, 1, 1), count); throw new Exception("Expected range rejection"); }
            catch (ArgumentOutOfRangeException) { }
        }
    });
    Test("backup export and restore preserve state", () => { var path = Path.Combine(folder, "backup.json"); CalendarStore.Export(new(1, [item]), path); store.Save(CalendarStore.Read(path)); Check(store.Load().Events.Single() == item); });
    Test("corrupt file is not overwritten on load", () => { File.WriteAllText(store.Path, "bad json"); try { store.Load(); throw new Exception("Expected failure"); } catch (System.Text.Json.JsonException) { } Check(File.ReadAllText(store.Path) == "bad json"); });
    Check(!Directory.GetFiles(folder, "*.tmp").Any());
    Console.WriteLine($"{count} tests passed");
}
finally { Directory.Delete(folder, true); }
