using System.Text.Json;
using AgentUp.Desktop.Features.Interface.Interfaces;
using AgentUp.Desktop.Features.Interface.Models;

namespace AgentUp.Desktop.Features.Interface.Providers;

/// <summary>
/// Stores which shell this client opens, beside the saved connections.
///
/// The file is read once at launch and written whenever the choice changes. A client that
/// cannot write it still runs; it opens on the classic shell every time, and Save reports
/// false so the surface offering the switch can say the choice will not survive a restart.
/// </summary>
public sealed class FileInterfacePreferenceStore : IInterfacePreferenceStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _filePath;

    public FileInterfacePreferenceStore()
        : this(Path.Join(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Agent-Up",
            "interface.json"))
    {
    }

    public FileInterfacePreferenceStore(string filePath)
    {
        _filePath = filePath;
    }

    public InterfacePreference Load()
    {
        if (!File.Exists(_filePath))
            return new InterfacePreference();

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<InterfacePreference>(json, Options) ?? new InterfacePreference();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // An unreadable preference is not a reason to refuse to start: fall back to the
            // shell that was there before this setting existed.
            return new InterfacePreference();
        }
    }

    public bool Save(InterfacePreference preference)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(preference, Options));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }
}
