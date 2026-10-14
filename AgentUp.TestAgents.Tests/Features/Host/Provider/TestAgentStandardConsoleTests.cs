using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json.Nodes;
using AgentUp.TestAgents.Composition;
using AgentUp.TestAgents.Features.Host.Controllers;
using AgentUp.TestAgents.Features.Host.Providers;

namespace AgentUp.TestAgents.Tests.Features.Host.Provider;

[TestFixture]
[CancelAfter(30_000)]
public sealed class TestAgentStandardConsoleTests
{
    [Test]
    public async Task FromStreams_returnsALineBeforeTheWriterIsClosed()
    {
        using var writer = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None);
        using var reader = new AnonymousPipeClientStream(PipeDirection.In, writer.ClientSafePipeHandle);
        var console = TestAgentStandardConsole.FromStreams(reader, Stream.Null, Stream.Null);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await writer.WriteAsync("""{"ok":true}"""u8.ToArray());
        await writer.WriteAsync("\n"u8.ToArray());
        await writer.FlushAsync(timeout.Token);

        Assert.That(await console.In.ReadLineAsync(timeout.Token), Is.EqualTo("""{"ok":true}"""));
    }

    [Test]
    public async Task FromStreams_writesUtf8LinesWithoutWaitingForTheReaderToClose()
    {
        using var output = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
        using var outputClient = new AnonymousPipeClientStream(PipeDirection.Out, output.ClientSafePipeHandle);
        using var error = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
        using var errorClient = new AnonymousPipeClientStream(PipeDirection.Out, error.ClientSafePipeHandle);
        var console = TestAgentStandardConsole.FromStreams(Stream.Null, outputClient, errorClient);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await console.Out.WriteLineAsync("stdout-line".AsMemory(), timeout.Token);
        await console.Error.WriteLineAsync("stderr-line".AsMemory(), timeout.Token);

        Assert.Multiple(() =>
        {
            Assert.That(ReadLine(output), Is.EqualTo("stdout-line"));
            Assert.That(ReadLine(error), Is.EqualTo("stderr-line"));
        });
    }

    [Test]
    public async Task HostProcess_answersInitializeWithoutClosingStdin()
    {
        var replies = await ExchangeAsync(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1}}""");

        Assert.Multiple(() =>
        {
            Assert.That(replies.Seed, Does.Contain("loop-inference seed="));
            Assert.That(JsonNode.Parse(replies.Stdout[0])!["result"]!["protocolVersion"]!.GetValue<int>(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task HostProcess_answersSessionNewWhileStdinStaysOpen()
    {
        var replies = await ExchangeAsync(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1}}""",
            """{"jsonrpc":"2.0","id":2,"method":"session/new","params":{"cwd":"."}}""");

        Assert.Multiple(() =>
        {
            Assert.That(replies.Stdout, Has.Count.EqualTo(2));
            Assert.That(
                JsonNode.Parse(replies.Stdout[1])!["result"]!["sessionId"]!.GetValue<string>(),
                Does.StartWith("infer-session-"));
        });
    }

    private static string ReadLine(AnonymousPipeServerStream stream)
    {
        using var reader = new StreamReader(stream, new UTF8Encoding(false), false, 1024, leaveOpen: true);
        return reader.ReadLine() ?? string.Empty;
    }

    private static async Task<HostReplies> ExchangeAsync(params string[] requests)
    {
        using var process = StartHost();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            var seed = await process.StandardError.ReadLineAsync(timeout.Token) ?? string.Empty;
            foreach (var request in requests)
                await process.StandardInput.WriteLineAsync(request.AsMemory(), timeout.Token);
            await process.StandardInput.FlushAsync(timeout.Token);

            var stdout = new List<string>();
            foreach (var _ in requests)
            {
                var line = await process.StandardOutput.ReadLineAsync(timeout.Token);
                if (line is null)
                    throw new InvalidOperationException("The test-agent host closed stdout before answering.");
                stdout.Add(line);
            }

            return new HostReplies(seed, stdout);
        }
        finally
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException)
            {
                TestContext.Out.WriteLine($"Could not stop the test-agent host: {exception.Message}");
            }
        }
    }

    private static Process StartHost()
    {
        var directory = Path.GetDirectoryName(typeof(TestAgentHostComposition).Assembly.Location)
            ?? throw new InvalidOperationException("Could not locate the test-agent host.");
        var unix = Path.Join(directory, "AgentUp.TestAgents");
        var windows = unix + ".exe";
        var dll = Path.Join(directory, "AgentUp.TestAgents.dll");
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var start = new ProcessStartInfo
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = utf8,
            StandardOutputEncoding = utf8,
            StandardErrorEncoding = utf8
        };
        if (OperatingSystem.IsWindows() && File.Exists(windows))
        {
            start.FileName = windows;
            start.ArgumentList.Add("--agent");
            start.ArgumentList.Add(TestAgentCommandParser.LocalInferenceName);
        }
        else if (File.Exists(unix))
        {
            start.FileName = unix;
            start.ArgumentList.Add("--agent");
            start.ArgumentList.Add(TestAgentCommandParser.LocalInferenceName);
        }
        else if (File.Exists(dll))
        {
            start.FileName = "dotnet";
            start.ArgumentList.Add("exec");
            start.ArgumentList.Add(dll);
            start.ArgumentList.Add("--agent");
            start.ArgumentList.Add(TestAgentCommandParser.LocalInferenceName);
        }
        else
        {
            throw new InvalidOperationException($"Could not locate the test-agent host in {directory}.");
        }

        start.Environment["AGENTUP_TEST_AGENT"] = TestAgentCommandParser.LocalInferenceName;
        start.Environment["AGENTUP_LOOP_INFERENCE_SEED"] = "7";
        var process = new Process { StartInfo = start };
        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException($"Could not start {start.FileName}.");
        }

        return process;
    }

    private sealed record HostReplies(string Seed, IReadOnlyList<string> Stdout);
}
