using AgentUp.Capabilities.Common.Features.NixRuntime.DTOs;
using AgentUp.Capabilities.Common.Features.NixRuntime.Models;

namespace AgentUp.Capabilities.Common.Features.NixRuntime.Providers;

public sealed class NixCapabilityCommandBuilder
{
    public NixLaunchWrap Wrap(string fileName, IReadOnlyList<string> arguments, NixEnvironmentSpec environment)
    {
        if (!string.IsNullOrWhiteSpace(environment.DefaultNixPath))
        {
            var command = string.Join(
                ' ',
                new[] { fileName }.Concat(arguments).Select(QuoteForShell));
            return new NixLaunchWrap("nix-shell", [environment.DefaultNixPath, "--run", command]);
        }

        if (environment.Packages.Count == 0)
            return new NixLaunchWrap(fileName, arguments);

        var packages = environment.Packages
            .Select(package => FlakePackage(environment.NixpkgsRev, package))
            .ToArray();
        return new NixLaunchWrap("nix", ["shell", .. packages, "--command", fileName, .. arguments]);
    }

    private static string QuoteForShell(string value)
        => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private static string FlakePackage(string? rev, string package)
        => string.IsNullOrWhiteSpace(rev)
            ? "nixpkgs#" + package
            : "github:NixOS/nixpkgs/" + rev + "#" + package;
}
