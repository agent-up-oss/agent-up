using System.Text.Json;
using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

namespace AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Providers;

public sealed class DotnetCapabilityPackageWriter
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public void Write(string packageDirectory, CapabilityPackageManifest manifest, string defaultNix)
        => Write(packageDirectory, manifest, defaultNix, AppContext.BaseDirectory);

    public void Write(string packageDirectory, CapabilityPackageManifest manifest, string defaultNix, string moduleDirectory)
    {
        Directory.CreateDirectory(packageDirectory);
        File.WriteAllText(Path.Join(packageDirectory, "capability.json"), JsonSerializer.Serialize(manifest, Json));
        File.WriteAllText(Path.Join(packageDirectory, "default.nix"), defaultNix);
        CopyModule(packageDirectory, moduleDirectory, manifest.Module);
    }

    private static void CopyModule(string packageDirectory, string moduleDirectory, string moduleFileName)
    {
        if (string.IsNullOrWhiteSpace(moduleFileName))
            return;

        foreach (var name in new[] { moduleFileName, "AgentUp.Sdk.Common.dll", "AgentUp.Sdk.Runtime.dll" })
        {
            var source = Path.Join(moduleDirectory, name);
            if (File.Exists(source))
                File.Copy(source, Path.Join(packageDirectory, name), overwrite: true);
        }
    }
}
