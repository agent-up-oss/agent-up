namespace AgentUp.Tray.Features.AutoStart;

/// <summary>
/// The value-format rules for the Windows Run key. Separate from the registrar because the
/// rules are pure string handling: keeping them here lets them be exercised anywhere, while
/// the registrar itself stays Windows-only.
/// </summary>
public static class WindowsRunKeyValue
{
    /// <summary>
    /// The Run-key value for an executable. Quoted so a path containing spaces is not read
    /// by Windows as a command plus arguments.
    /// </summary>
    public static string Format(string exePath) => $"\"{exePath}\"";

    /// <summary>
    /// Whether a stored Run-key value already points at this executable. Case-insensitive
    /// because Windows paths are.
    /// </summary>
    public static bool Matches(string? registered, string exePath)
        => registered is not null
           && string.Equals(registered, Format(exePath), StringComparison.OrdinalIgnoreCase);
}
