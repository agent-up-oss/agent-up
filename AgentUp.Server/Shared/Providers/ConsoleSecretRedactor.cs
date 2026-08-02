using System.Text.RegularExpressions;

namespace AgentUp.Server.Shared.Providers;

public sealed partial class ConsoleSecretRedactor
{
    public string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        var redacted = SecretAssignmentPattern().Replace(value, match =>
            $"{match.Groups["key"].Value}{match.Groups["separator"].Value}[REDACTED]");
        return UriCredentialPattern().Replace(redacted, "${scheme}[REDACTED]@");
    }

    [GeneratedRegex(
        @"(?<key>(?:password|passwd|pwd|secret|token|api[_-]?key|access[_-]?token|refresh[_-]?token|connectionstring))(?<separator>\s*[:=]\s*)[^\s;]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretAssignmentPattern();

    [GeneratedRegex(
        @"(?<scheme>[a-z][a-z0-9+\-.]*://)[^/\s:@]+:[^/\s@]+@",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UriCredentialPattern();
}
