namespace AgentUp.Server.Shared.Providers;

/// <summary>
/// Makes a caller-supplied identifier safe to write into a log line.
/// </summary>
/// <remarks>
/// Workspace and application identifiers reach the Server from REST routes and MCP arguments.
/// Structured logging keeps them out of the message template, but the rendered line is still one
/// line of text: an identifier carrying a newline writes a second line that reads like a genuine
/// log entry. Control characters are replaced rather than stripped so a forged identifier stays
/// visible in the line it forged.
/// </remarks>
public static class LogSafeIdentifier
{
    private const int MaxLength = 200;

    public static string Of(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        var source = value.Length > MaxLength ? value[..MaxLength] : value;
        return string.Create(source.Length, source, static (destination, text) =>
        {
            for (var index = 0; index < text.Length; index++)
            {
                var character = text[index];
                destination[index] = char.IsControl(character) ? '�' : character;
            }
        });
    }
}
