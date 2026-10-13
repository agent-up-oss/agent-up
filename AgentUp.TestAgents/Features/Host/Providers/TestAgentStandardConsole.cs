using System.Text;
using AgentUp.TestAgents.Features.Host.Models;

namespace AgentUp.TestAgents.Features.Host.Providers;

/// <summary>
/// ACP stdio over the process pipes, not <see cref="Console.In"/>. Console's reader waits for a
/// 4 KiB fill or EOF on a redirected pipe, so a Server that writes one JSON-RPC line and keeps
/// stdin open would deadlock waiting for initialize.
/// </summary>
public static class TestAgentStandardConsole
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public static TestAgentConsole FromStreams(Stream input, Stream output, Stream error) => new(
        new StreamReader(input, Utf8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true),
        Writer(output),
        Writer(error));

    private static StreamWriter Writer(Stream stream) =>
        new(stream, Utf8, bufferSize: 1024, leaveOpen: true) { AutoFlush = true };
}
