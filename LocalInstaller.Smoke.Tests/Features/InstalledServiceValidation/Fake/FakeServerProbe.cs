using LocalInstaller.Smoke.Features.InstalledServiceValidation.Interfaces;
using LocalInstaller.Smoke.Shared.Providers;

namespace LocalInstaller.Smoke.Tests.Features.InstalledServiceValidation.Fake;

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
            var safeOutputFile = SafeSmokePaths.Child(Path.GetDirectoryName(outputFile)!, Path.GetFileName(outputFile));
            Directory.CreateDirectory(Path.GetDirectoryName(safeOutputFile)!);
            File.WriteAllText(safeOutputFile, "[]");
        }

        return Task.FromResult(_readyUrl);
    }
}
