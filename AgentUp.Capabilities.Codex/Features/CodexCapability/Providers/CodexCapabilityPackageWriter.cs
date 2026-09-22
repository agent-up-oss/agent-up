using System.Text.Json;
using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

namespace AgentUp.Capabilities.Codex.Features.CodexCapability.Providers;

public sealed class CodexCapabilityPackageWriter
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public void Write(string packageDirectory, CapabilityPackageManifest manifest, string defaultNix)
        => Write(packageDirectory, manifest, defaultNix, AppContext.BaseDirectory);

    public void Write(string packageDirectory, CapabilityPackageManifest manifest, string defaultNix, string moduleDirectory)
    {
        Directory.CreateDirectory(packageDirectory);
        File.WriteAllText(Path.Join(packageDirectory, "capability.json"), JsonSerializer.Serialize(manifest, Json));
        File.WriteAllText(Path.Join(packageDirectory, "default.nix"), defaultNix);
        CopyModule(packageDirectory, moduleDirectory, manifest.Module, "AgentUp.Sdk.Common.dll", "AgentUp.Sdk.Agent.dll");
    }

    private static void CopyModule(string packageDirectory, string moduleDirectory, params string?[] names)
    {
        foreach (var name in names.Where(name => !string.IsNullOrWhiteSpace(name)))
        {
            var source = Path.Join(moduleDirectory, name);
            if (File.Exists(source))
                File.Copy(source, Path.Join(packageDirectory, name!), overwrite: true);
        }
    }
}
