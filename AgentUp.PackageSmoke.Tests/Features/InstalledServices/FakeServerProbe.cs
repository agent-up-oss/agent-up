using AgentUp.PackageSmoke.Features.InstalledServices;

namespace AgentUp.PackageSmoke.Tests.Features.InstalledServices;

internal sealed class FakeServerProbe : IServerProbe
{
    private readonly string? _readyUrl;

    public FakeServerProbe(string? readyUrl)
    {
        _readyUrl = readyUrl;
    }

    public List<(string PrimaryUrl, string FallbackUrl, string OutputFile)> Calls { get; } = [];

    public Task<string?> WaitForReadyAsync(string primaryUrl, string fallbackUrl, string outputFile, CancellationToken cancellationToken = default)
    {
        Calls.Add((primaryUrl, fallbackUrl, outputFile));
        if (_readyUrl is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
            File.WriteAllText(outputFile, "[]");
        }

        return Task.FromResult(_readyUrl);
    }
}
