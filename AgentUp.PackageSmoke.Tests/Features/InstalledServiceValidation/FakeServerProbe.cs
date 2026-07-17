using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;
using AgentUp.PackageSmoke.Features.PackageValidation.Factories;
using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Providers;

namespace AgentUp.PackageSmoke.Tests.Features.InstalledServiceValidation;

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
