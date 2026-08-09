using LocalInstaller.Smoke.Features.PackageValidation.DTOs;
using LocalInstaller.Smoke.Features.SmokeRuns.Interfaces;

namespace LocalInstaller.Smoke.Features.SmokeRuns.Providers;

public sealed class SmokeWorkDirectoryProvider : ISmokeWorkDirectoryProvider
{
    public void Prepare(string workDirectory)
    {
        if (Directory.Exists(workDirectory))
            Directory.Delete(workDirectory, recursive: true);
        Directory.CreateDirectory(workDirectory);
    }

    public Task WritePackageEnvironmentAsync(
        string workDirectory,
        PackageValidationResult result,
        CancellationToken cancellationToken = default)
        => File.WriteAllTextAsync(Path.Join(workDirectory, "package-smoke.env"), result.ToEnvironmentFile(), cancellationToken);
}
