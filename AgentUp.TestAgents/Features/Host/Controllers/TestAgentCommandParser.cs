using AgentUp.TestAgents.Features.Host.Models;

namespace AgentUp.TestAgents.Features.Host.Controllers;

/// <summary>
/// The command-line surface of the test agents. One executable is published per schema, so the
/// schema comes from the program name the way <c>codex</c> and <c>claude</c> differ, and the verb
/// comes from the arguments the Server passes: <c>acp</c>, <c>login</c>, or <c>setup-token</c>.
/// </summary>
public static class TestAgentCommandParser
{
    public const string LoopbackRedirectName = "test-agent1";
    public const string DeviceCodeName = "test-agent2";
    public const string PastedCodeName = "test-agent3";
    public const string SilentPollName = "test-agent4";
    public const string IdentityProviderName = "test-idp";

    public static TestAgentCommand Parse(string programName, IReadOnlyList<string> arguments)
    {
        var name = ResolveName(programName, arguments);
        var schema = ReadSchema(name);
        var verb = IsIdentityProvider(name) ? TestAgentVerb.IdentityProvider : ReadVerb(arguments);
        return new TestAgentCommand(
            schema,
            verb,
            ReadOption(arguments, "--idp") ?? Environment.GetEnvironmentVariable("AGENTUP_TEST_IDP_URL"),
            ReadPort(arguments),
            ReadOption(arguments, "--public-origin") ?? Environment.GetEnvironmentVariable("AGENTUP_TEST_IDP_PUBLIC_ORIGIN"));
    }

    /// <summary>
    /// Which agent this process is. One binary is published and launched through a per-agent
    /// shim, so the name is taken from the shim rather than from the executable path: a renamed
    /// or symlinked apphost does not reliably report the name it was invoked under.
    /// </summary>
    internal static string ResolveName(string programName, IReadOnlyList<string> arguments) =>
        ReadOption(arguments, "--agent")
        ?? Environment.GetEnvironmentVariable("AGENTUP_TEST_AGENT")
        ?? programName;

    internal static bool IsIdentityProvider(string programName) =>
        Normalize(programName).StartsWith(IdentityProviderName, StringComparison.Ordinal);

    internal static TestAgentSchema ReadSchema(string programName)
    {
        var name = Normalize(programName);
        if (name.StartsWith(LoopbackRedirectName, StringComparison.Ordinal)) return TestAgentSchema.LoopbackRedirect;
        if (name.StartsWith(DeviceCodeName, StringComparison.Ordinal)) return TestAgentSchema.DeviceCode;
        if (name.StartsWith(PastedCodeName, StringComparison.Ordinal)) return TestAgentSchema.PastedCode;
        if (name.StartsWith(SilentPollName, StringComparison.Ordinal)) return TestAgentSchema.SilentPoll;
        if (name.StartsWith(IdentityProviderName, StringComparison.Ordinal)) return TestAgentSchema.DeviceCode;
        throw new InvalidOperationException(
            $"'{programName}' is not a test agent. Publish it as {LoopbackRedirectName}, {DeviceCodeName}, {PastedCodeName}, {SilentPollName}, or {IdentityProviderName}.");
    }

    internal static TestAgentVerb ReadVerb(IReadOnlyList<string> arguments)
    {
        foreach (var argument in arguments)
        {
            // setup-token is the claude spelling; login is the codex and cursor spelling.
            if (argument.Equals("login", StringComparison.OrdinalIgnoreCase)
                || argument.Equals("setup-token", StringComparison.OrdinalIgnoreCase))
                return TestAgentVerb.Login;
            if (argument.Equals("acp", StringComparison.OrdinalIgnoreCase))
                return TestAgentVerb.Acp;
        }

        return TestAgentVerb.Acp;
    }

    internal static int ReadPort(IReadOnlyList<string> arguments)
    {
        var value = ReadOption(arguments, "--port") ?? Environment.GetEnvironmentVariable("AGENTUP_TEST_IDP_PORT");
        return int.TryParse(value, out var port) && port is > 0 and <= 65535 ? port : 0;
    }

    internal static string? ReadOption(IReadOnlyList<string> arguments, string name)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            if (arguments[index].StartsWith($"{name}=", StringComparison.Ordinal))
                return arguments[index][(name.Length + 1)..];
            if (arguments[index].Equals(name, StringComparison.Ordinal) && index + 1 < arguments.Count)
                return arguments[index + 1];
        }

        return null;
    }

    private static string Normalize(string programName)
    {
        var name = Path.GetFileNameWithoutExtension(programName);
        return string.IsNullOrWhiteSpace(name) ? string.Empty : name.ToLowerInvariant();
    }
}
