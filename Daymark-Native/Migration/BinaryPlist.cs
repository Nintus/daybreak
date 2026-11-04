using System.Text;

namespace Daymark.Migration;

// Bounded metadata reader. Data/date/real objects are deliberately not materialized.
public static class BinaryPlist
{
    public static object? Read(byte[] data)
    {
        if (data.Length is < 40 or > 4_000_000 || Encoding.ASCII.GetString(data, 0, 8) != "bplist00") throw new InvalidDataException("Unsupported account metadata plist.");
        long Number(int offset, int size)
        {
            if (size is < 1 or > 8 || offset < 0 || offset > data.Length - size) throw new InvalidDataException("Invalid plist offset.");
            ulong value = 0; for (var i = 0; i < size; i++) value = (value << 8) | data[offset + i];
            if (value > int.MaxValue) throw new InvalidDataException("Plist value exceeds metadata bounds."); return (long)value;
        }
        var trailer = data.Length - 32; var offsetsSize = data[trailer + 6]; var refsSize = data[trailer + 7];
        var count = (int)Number(trailer + 8, 8); var root = (int)Number(trailer + 16, 8); var table = (int)Number(trailer + 24, 8);
        if (offsetsSize is < 1 or > 8 || refsSize is < 1 or > 8 || count is < 1 or > 100000 || root >= count || table < 8 || (long)table + (long)count * offsetsSize > trailer) throw new InvalidDataException("Invalid plist object table.");
        var visiting = new HashSet<int>(); var memo = new Dictionary<int, object?>();
        object? Object(int index, int depth)
        {
            if (index < 0 || index >= count || depth > 32 || !visiting.Add(index)) throw new InvalidDataException("Invalid cyclic plist.");
            try
            {
                if (memo.TryGetValue(index, out var saved)) return saved;
                var offset = (int)Number(table + index * offsetsSize, offsetsSize); if (offset < 8 || offset >= table) throw new InvalidDataException("Invalid plist object.");
                var marker = data[offset++]; var type = marker >> 4; var length = marker & 15;
                if (type is 4 or 5 or 6 or 10 or 13 && length == 15)
                {
                    if (offset >= table) throw new InvalidDataException("Truncated plist length.");
                    var size = data[offset++]; if ((size >> 4) != 1 || (size & 15) > 3) throw new InvalidDataException("Invalid plist length.");
                    length = (int)Number(offset, 1 << (size & 15)); offset += 1 << (size & 15);
                }
                if (length > 100000) throw new InvalidDataException("Oversized plist object.");
                int Ref(int position) => (int)Number(position, refsSize);
                object? value = null;
                if (type == 0) value = marker == 9 ? true : marker == 8 ? false : null;
                else if (type is 5 or 6)
                {
                    var bytes = checked(length * (type == 6 ? 2 : 1)); if (offset > table - bytes) throw new InvalidDataException("Truncated plist string.");
                    value = (type == 6 ? Encoding.BigEndianUnicode : Encoding.ASCII).GetString(data, offset, bytes);
                }
                else if (type == 10)
                {
                    if (length > 1000 || (long)offset + (long)length * refsSize > table) throw new InvalidDataException("Invalid plist array.");
                    value = Enumerable.Range(0, length).Select(i => Object(Ref(offset + i * refsSize), depth + 1)).ToList();
                }
                else if (type == 13)
                {
                    if (length > 1000 || (long)offset + 2L * length * refsSize > table) throw new InvalidDataException("Invalid plist dictionary.");
                    var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
                    for (var i = 0; i < length; i++)
                    {
                        var key = Object(Ref(offset + i * refsSize), depth + 1) as string ?? throw new InvalidDataException("Invalid plist key.");
                        // Ignore credential values even if a future metadata schema includes them.
                        if (key.Contains("token", StringComparison.OrdinalIgnoreCase) || key.Contains("password", StringComparison.OrdinalIgnoreCase) || key.Contains("secret", StringComparison.OrdinalIgnoreCase)) continue;
                        dict[key] = Object(Ref(offset + (length + i) * refsSize), depth + 1);
                    }
                    value = dict;
                }
                memo[index] = value; return value;
            }
            finally { visiting.Remove(index); }
        }
        return Object(root, 0);
    }
    public static List<AccountDraft> Accounts(byte[] data, string source)
    {
        var output = new List<AccountDraft>();
        if (Read(data) is not Dictionary<string, object?> root) return output;
        foreach (var row in root.Values.OfType<Dictionary<string, object?>>())
        {
            string Value(string key) => row.GetValueOrDefault(key) as string ?? "";
            if (Value("Type") is "flexibits" or "todoist") continue;
            var login = Value("EmailAddress"); if (login.Length == 0) login = Value("Username");
            if (ImportIdentity.Account(source, Value("Name"), login, Value("ServerURL"), Value("Type")) is { } account) output.Add(account);
        }
        return output;
    }
}
