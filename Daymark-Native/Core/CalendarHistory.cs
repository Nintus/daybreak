namespace Daymark.Core;

// Persist before changing either the visible document or the history stacks.
public sealed class CalendarHistory
{
    private sealed record Entry(CalendarDocument Document, string Description);
    private CalendarDocument current;
    private readonly List<Entry> undo = [];
    private readonly List<Entry> redo = [];
    private readonly int capacity;

    public CalendarHistory(CalendarDocument initial, int capacity = 20)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        initial.Validate();
        current = Copy(initial); this.capacity = capacity;
    }

    public CalendarDocument Current => Copy(current);
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public string? UndoDescription => CanUndo ? undo[^1].Description : null;
    public string? RedoDescription => CanRedo ? redo[^1].Description : null;

    public void Commit(CalendarDocument next, string description, Action<CalendarDocument> persist)
    {
        next.Validate();
        var snapshot = Copy(next);
        if (current.Version == snapshot.Version && current.Events.SequenceEqual(snapshot.Events)) return;
        persist(Copy(snapshot));
        undo.Add(new(current, description));
        if (undo.Count > capacity) undo.RemoveAt(0);
        redo.Clear(); current = snapshot;
    }

    public bool Undo(Action<CalendarDocument> persist) => Move(undo, redo, persist);
    public bool Redo(Action<CalendarDocument> persist) => Move(redo, undo, persist);

    private bool Move(List<Entry> from, List<Entry> to, Action<CalendarDocument> persist)
    {
        if (from.Count == 0) return false;
        var entry = from[^1];
        persist(Copy(entry.Document));
        to.Add(new(current, entry.Description));
        current = entry.Document; from.RemoveAt(from.Count - 1);
        return true;
    }

    private static CalendarDocument Copy(CalendarDocument document) => new(document.Version, [.. document.Events]);
}
