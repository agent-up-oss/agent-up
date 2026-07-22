namespace AgentUp.PackageSmoke.Features.InstalledServiceValidation.Models;

public sealed record SmokeProductConfig
{
    public SmokeProductConfig(
        string ServiceName,
        string CliShimName,
        string ArtifactBaseName,
        string DisplayName,
        string InstallDirName,
        string WorkspaceConfigFileName = "agent-up.json")
    {
        ValidateSafeIdentifier(ServiceName, nameof(ServiceName));
        ValidateSafeIdentifier(CliShimName, nameof(CliShimName));
        ValidateSafeIdentifier(ArtifactBaseName, nameof(ArtifactBaseName));
        ValidateDisplayName(DisplayName, nameof(DisplayName));
        ValidatePathComponent(InstallDirName, nameof(InstallDirName));
        ValidateConfigFileName(WorkspaceConfigFileName, nameof(WorkspaceConfigFileName));

        this.ServiceName = ServiceName;
        this.CliShimName = CliShimName;
        this.ArtifactBaseName = ArtifactBaseName;
        this.DisplayName = DisplayName;
        this.InstallDirName = InstallDirName;
        this.WorkspaceConfigFileName = WorkspaceConfigFileName;
    }

    public string ServiceName { get; }

    public string CliShimName { get; }

    public string ArtifactBaseName { get; }

    public string DisplayName { get; }

    public string InstallDirName { get; }

    public string WorkspaceConfigFileName { get; }

    public static readonly SmokeProductConfig AgentUp = new(
        ServiceName: "agent-up-server",
        CliShimName: "agent-up",
        ArtifactBaseName: "agent-up",
        DisplayName: "Agent-Up",
        InstallDirName: "Agent-Up");

    private static void ValidateSafeIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim() || value[0] == '-' || value[^1] == '-')
            throw InvalidProductValue(parameterName, "a non-empty identifier that starts and ends with a lowercase ASCII letter or digit");

        if (value.Any(character => !IsAsciiLowercaseLetter(character) && !char.IsAsciiDigit(character) && character != '-'))
            throw InvalidProductValue(parameterName, "lowercase ASCII letters, digits, and hyphens only");
    }

    private static void ValidateDisplayName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
            throw InvalidProductValue(parameterName, "non-empty display text without leading or trailing whitespace");

        if (value.IndexOfAny(['\0', '\r', '\n']) >= 0)
            throw InvalidProductValue(parameterName, "display text without control characters");
    }

    private static void ValidatePathComponent(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim() || value[0] is '-' or '.' || !char.IsAsciiLetterOrDigit(value[^1]))
            throw InvalidProductValue(parameterName, "a non-empty path component that starts and ends safely");

        if (value.Contains("..", StringComparison.Ordinal) || value.IndexOfAny(['/', '\\', ':']) >= 0)
            throw InvalidProductValue(parameterName, "a single path component without traversal or separators");

        if (value.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ' ' and not '-' and not '_' and not '.'))
            throw InvalidProductValue(parameterName, "ASCII letters, digits, spaces, hyphens, underscores, and periods only");
    }

    private static void ValidateConfigFileName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value != value.Trim() ||
            value[0] is '-' or '.' ||
            !value.EndsWith(".json", StringComparison.Ordinal))
        {
            throw InvalidProductValue(parameterName, "a safe repository-local .json file name");
        }

        if (value.Contains("..", StringComparison.Ordinal) || value.IndexOfAny(['/', '\\', ':']) >= 0)
            throw InvalidProductValue(parameterName, "a file name without traversal or separators");

        if (value.Any(character => !IsAsciiLowercaseLetter(character) && !char.IsAsciiDigit(character) && character is not '-' and not '_' and not '.'))
            throw InvalidProductValue(parameterName, "lowercase ASCII letters, digits, hyphens, underscores, and periods only");
    }

    private static bool IsAsciiLowercaseLetter(char character)
        => character is >= 'a' and <= 'z';

    private static ArgumentException InvalidProductValue(string parameterName, string expected)
        => new($"Smoke product {parameterName} must be {expected}.", parameterName);
}
