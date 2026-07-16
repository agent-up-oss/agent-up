using System.Text.Json;

namespace AgentUp.Desktop.Features.FirstRun.Services;

public sealed class FileFirstRunTutorialSettingsStore : IFirstRunTutorialSettingsStore
{
    public const string SkipTutorialEnvironmentVariable = "AGENTUP_SKIP_FIRST_RUN_TUTORIAL";
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _settingsPath;

    public FileFirstRunTutorialSettingsStore()
        : this(Path.Join(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Agent-Up",
            "user-settings.json"))
    {
    }

    internal FileFirstRunTutorialSettingsStore(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    public async Task<FirstRunTutorialSettings> LoadAsync()
    {
        if (ShouldSkipFromEnvironment())
            return new FirstRunTutorialSettings(false, true, 0);

        if (!File.Exists(_settingsPath))
            return new FirstRunTutorialSettings(false, false, 0);

        try
        {
            await using var stream = File.OpenRead(_settingsPath);
            return await JsonSerializer.DeserializeAsync<FirstRunTutorialSettings>(stream)
                   ?? new FirstRunTutorialSettings(false, false, 0);
        }
        catch (JsonException)
        {
            return new FirstRunTutorialSettings(false, false, 0);
        }
        catch (IOException)
        {
            return new FirstRunTutorialSettings(false, false, 0);
        }
    }

    public async Task SaveAsync(FirstRunTutorialSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, Options);
    }

    private static bool ShouldSkipFromEnvironment()
    {
        var value = Environment.GetEnvironmentVariable(SkipTutorialEnvironmentVariable);
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}
