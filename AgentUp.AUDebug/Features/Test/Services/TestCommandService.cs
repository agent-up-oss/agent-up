using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Test.DTOs;
using AgentUp.AUDebug.Features.Test.Interfaces;

namespace AgentUp.AUDebug.Features.Test.Services;

public sealed class TestCommandService
{
    private readonly IDebugTestSuiteCatalog _catalog;
    private readonly IDebugTestProcessRunner _runner;
    private readonly TextWriter _output;

    public TestCommandService(
        IDebugTestSuiteCatalog catalog,
        IDebugTestProcessRunner runner,
        TextWriter output)
    {
        _catalog = catalog;
        _runner = runner;
        _output = output;
    }

    public async Task<CommandResultDto> RunAsync(DebugCommandDto command, CancellationToken cancellationToken)
    {
        var suite = command.Suite ?? "all";
        if (!_catalog.TryResolve(suite, out var suites, out var error))
            return CommandResultDto.Fail(error!);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(command.Timeout);
        try
        {
            var passed = new List<string>();
            foreach (var item in suites)
            {
                await _output.WriteLineAsync($"au-debug test {item.Id}: {item.Title}");
                var failure = await RunSuiteAsync(item, timeout.Token);
                if (failure is not null)
                    return CommandResultDto.Fail(failure);
                passed.Add(item.Id);
            }

            return CommandResultDto.Ok($"Passed {passed.Count} test suite(s): {string.Join(", ", passed)}.");
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return CommandResultDto.Fail($"Timed out after {(int)command.Timeout.TotalSeconds}s running au-debug test {suite}.");
        }
    }

    private async Task<string?> RunSuiteAsync(DebugTestSuiteDto suite, CancellationToken cancellationToken)
    {
        foreach (var step in suite.Steps)
        {
            var result = await _runner.RunAsync(step, cancellationToken);
            var transcript = Combine(result.StandardOutput, result.StandardError);
            if (!string.IsNullOrWhiteSpace(transcript))
                await _output.WriteLineAsync(transcript.TrimEnd());
            if (result.ExitCode != 0)
                return $"Suite '{suite.Id}' failed ({result.ExitCode}).";
        }

        return null;
    }

    private static string Combine(string stdout, string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
            return stdout;
        if (string.IsNullOrWhiteSpace(stdout))
            return stderr;
        return stdout.TrimEnd() + Environment.NewLine + stderr;
    }
}
