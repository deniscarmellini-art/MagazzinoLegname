using System.IO;
using System.Text.Json;

namespace MagazzinoLegname.Services;

public enum PackageTerminalState { Discharged, Returned, ManuallyRemoved, SupplementaryExited }

public sealed record PackageTerminalStateRecord(
    string PackageCode, PackageTerminalState State, DateTime RecordedAt, string Operator);

public sealed class PackageTerminalStateStore
{
    private readonly object _sync = new();
    private readonly Dictionary<string, PackageTerminalStateRecord> _states;
    private readonly string _filePath;
    public static PackageTerminalStateStore Shared { get; } = new();

    private PackageTerminalStateStore()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MagazzinoLegname");
        _filePath = Path.Combine(directory, "package-terminal-states.json");
        _states = Load(_filePath);
    }

    public PackageTerminalStateRecord? Find(string packageCode)
    {
        lock (_sync) return _states.GetValueOrDefault(packageCode.Trim());
    }

    public void Record(string packageCode, PackageTerminalState state, DateTime recordedAt, string operatorName)
    {
        lock (_sync)
        {
            var code = packageCode.Trim().ToUpperInvariant();
            _states[code] = new(code, state, recordedAt, operatorName);
            var directory = Path.GetDirectoryName(_filePath)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = _filePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(_states.Values.OrderBy(item => item.PackageCode),
                new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryPath, _filePath, true);
        }
    }

    public void ResetTestData()
    {
        lock (_sync)
        {
            _states.Clear();
            if (File.Exists(_filePath)) File.Delete(_filePath);
        }
    }

    private static Dictionary<string, PackageTerminalStateRecord> Load(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return new(StringComparer.OrdinalIgnoreCase);
            var records = JsonSerializer.Deserialize<List<PackageTerminalStateRecord>>(File.ReadAllText(filePath)) ?? [];
            return records.ToDictionary(item => item.PackageCode, StringComparer.OrdinalIgnoreCase);
        }
        catch (IOException) { return new(StringComparer.OrdinalIgnoreCase); }
        catch (JsonException) { return new(StringComparer.OrdinalIgnoreCase); }
        catch (UnauthorizedAccessException) { return new(StringComparer.OrdinalIgnoreCase); }
    }
}
