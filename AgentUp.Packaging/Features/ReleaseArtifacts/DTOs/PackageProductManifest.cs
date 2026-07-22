namespace AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

public sealed record PackageProductManifest
{
    private static readonly char[] WindowsInvalidFileNameChars = ['<', '>', '"', '|', '?', '*'];
    private static readonly string[] WindowsReservedDeviceNames =
    [
        "CON",
        "PRN",
        "AUX",
        "NUL",
        "COM1",
        "COM2",
        "COM3",
        "COM4",
        "COM5",
        "COM6",
        "COM7",
        "COM8",
        "COM9",
        "LPT1",
        "LPT2",
        "LPT3",
        "LPT4",
        "LPT5",
        "LPT6",
        "LPT7",
        "LPT8",
        "LPT9"
    ];

    private static readonly PackageProductManifest AgentUpManifest = new("Agent-Up", "agent-up", "AGENTUP")
    {
        Manufacturer = "Agent-Up",
        WindowsUpgradeCode = "5E8FB224-E5E3-4D48-8B62-2F50D521CBB0"
    };

    public PackageProductManifest(
        string productName,
        string slug,
        string environmentPrefix)
    {
        ProductName = RequireSafeWindowsPathComponent(productName, nameof(ProductName));
        Slug = global::AgentUp.Packaging.Shared.Providers.PackagePathValidator.RequireSafePathComponent(slug, nameof(Slug));
        EnvironmentPrefix = RequireEnvironmentPrefix(environmentPrefix, nameof(EnvironmentPrefix));
    }

    public string ProductName { get; init; }
    public string Slug { get; init; }
    public string EnvironmentPrefix { get; init; }
    public string? Manufacturer { get; init; }
    public string? WindowsUpgradeCode { get; init; }
    public string? WindowsServiceName { get; init; }
    public string? WindowsCliShimName { get; init; }
    public string? WindowsServerUrl { get; init; }

    public static PackageProductManifest AgentUp()
        => AgentUpManifest;

    public bool IsValid()
        => IsValidOptionalWindowsPathComponent(Manufacturer)
           && IsValidOptionalGuid(WindowsUpgradeCode)
           && IsValidOptionalWindowsServiceName(WindowsServiceName)
           && IsValidOptionalWindowsFileName(WindowsCliShimName)
           && IsValidOptionalServerUrl(WindowsServerUrl);

    public static void Validate(PackageProductManifest manifest)
    {
        _ = RequireSafeWindowsPathComponent(manifest.ProductName, nameof(ProductName));
        _ = global::AgentUp.Packaging.Shared.Providers.PackagePathValidator.RequireSafePathComponent(manifest.Slug, nameof(Slug));
        _ = RequireEnvironmentPrefix(manifest.EnvironmentPrefix, nameof(EnvironmentPrefix));

        if (!IsValidOptionalWindowsPathComponent(manifest.Manufacturer))
            throw new ArgumentException("Manufacturer must be a safe Windows path component when specified.", nameof(Manufacturer));
        if (!IsValidOptionalGuid(manifest.WindowsUpgradeCode))
            throw new ArgumentException("Windows upgrade code must be a valid GUID when specified.", nameof(WindowsUpgradeCode));
        if (!IsValidOptionalWindowsServiceName(manifest.WindowsServiceName))
            throw new ArgumentException("Windows service name must be a safe service identifier when specified.", nameof(WindowsServiceName));
        if (!IsValidOptionalWindowsFileName(manifest.WindowsCliShimName))
            throw new ArgumentException("Windows CLI shim name must be a safe Windows file name when specified.", nameof(WindowsCliShimName));
        if (!IsValidOptionalServerUrl(manifest.WindowsServerUrl))
            throw new ArgumentException("Windows server URL must be an absolute HTTP or HTTPS URL when specified.", nameof(WindowsServerUrl));
    }

    public string RequireValidWindowsCliShimName(string value)
        => RequireSafeWindowsFileName(value, nameof(WindowsCliShimName));

    private static bool IsValidOptionalWindowsPathComponent(string? value)
        => value is null || Try(() => RequireSafeWindowsPathComponent(value, nameof(value)));

    private static bool IsValidOptionalGuid(string? value)
        => value is null || Guid.TryParseExact(value, "D", out _);

    private static bool IsValidOptionalWindowsServiceName(string? value)
        => value is null || Try(() => RequireWindowsServiceName(value, nameof(value)));

    private static bool IsValidOptionalWindowsFileName(string? value)
        => value is null || Try(() => RequireSafeWindowsFileName(value, nameof(value)));

    private static bool IsValidOptionalServerUrl(string? value)
        => value is null || Uri.TryCreate(value, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
           && !string.IsNullOrWhiteSpace(uri.Host);

    private static string RequireSafeWindowsPathComponent(string value, string paramName)
    {
        RequireNonEmpty(value, paramName);
        if (!IsSafeWindowsFileName(value))
            throw new ArgumentException("Value must be a safe Windows path component.", paramName);

        return value;
    }

    private static string RequireSafeWindowsFileName(string value, string paramName)
    {
        RequireNonEmpty(value, paramName);
        if (!IsSafeWindowsFileName(value))
            throw new ArgumentException("Value must be a safe Windows file name.", paramName);

        return value;
    }

    private static string RequireWindowsServiceName(string value, string paramName)
    {
        RequireNonEmpty(value, paramName);
        if (value.Any(char.IsControl)
            || value.Contains('"', StringComparison.Ordinal)
            || value.Contains('/', StringComparison.Ordinal)
            || value.Contains('\\', StringComparison.Ordinal))
            throw new ArgumentException("Value must be a safe Windows service name.", paramName);

        return value;
    }

    private static string RequireEnvironmentPrefix(string value, string paramName)
    {
        RequireNonEmpty(value, paramName);
        if (!value.All(ch => char.IsAsciiLetterUpper(ch) || char.IsAsciiDigit(ch) || ch == '_'))
            throw new ArgumentException("Environment prefix must contain only uppercase ASCII letters, digits, or underscores.", paramName);

        return value;
    }

    private static void RequireNonEmpty(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value must not be empty.", paramName);
    }

    private static bool IsSafeWindowsFileName(string value)
    {
        if (value is "." or ".." || value.EndsWith(' ') || value.EndsWith('.'))
            return false;

        if (value.Contains('/', StringComparison.Ordinal)
            || value.Contains('\\', StringComparison.Ordinal)
            || value.Contains(':', StringComparison.Ordinal)
            || value.IndexOfAny(WindowsInvalidFileNameChars) >= 0
            || value.Any(char.IsControl)
            || System.IO.Path.IsPathFullyQualified(value)
            || !System.IO.Path.GetFileName(value).Equals(value, StringComparison.Ordinal))
            return false;

        var baseName = value.Split('.')[0];
        return !WindowsReservedDeviceNames.Contains(baseName, StringComparer.OrdinalIgnoreCase);
    }

    private static bool Try(Func<string> validate)
    {
        try
        {
            _ = validate();
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
