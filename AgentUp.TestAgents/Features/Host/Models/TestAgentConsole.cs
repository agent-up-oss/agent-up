namespace AgentUp.TestAgents.Features.Host.Models;

/// <summary>
/// The streams the process talks to the world through.
/// <para>
/// Passed in rather than reached for, because these agents are driven entirely over stdio: a host
/// that named <c>Console</c> itself could only be exercised by redirecting the whole process's
/// streams, which is global state a test has to put back.
/// </para>
/// </summary>
public sealed record TestAgentConsole(TextReader In, TextWriter Out, TextWriter Error);
