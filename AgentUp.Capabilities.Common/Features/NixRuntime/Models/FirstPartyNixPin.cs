using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

namespace AgentUp.Capabilities.Common.Features.NixRuntime.Models;

public static class FirstPartyNixPin
{
    public const string Rev = "b134951a4c9f3c995fd7be05f9a8dafa8c4ffb90";

    // The commit is the pin. First-party packages record no sha256 rather than the placeholder
    // they used to ship to satisfy a check that nothing verified.
    public static CapabilityNixpkgsPin Nixpkgs { get; } = new() { Rev = Rev };

    public static string DefaultNix(IReadOnlyList<string> packages) => DefaultNix(packages, Rev);

    /// <summary>
    /// The packed shell a capability package carries, pinned to the nixpkgs commit the package
    /// records.
    /// </summary>
    /// <remarks>
    /// A package is installed into whatever Server enables it, so it cannot assume that host
    /// configured a nixpkgs channel. <c>import &lt;nixpkgs&gt;</c> resolves through NIX_PATH, and a
    /// Nix installed by the Server image or by the upstream install script has none - every
    /// wrapped launch then fails in the Nix evaluator rather than in anything the operator can
    /// see. Fetching the recorded commit makes the package self-contained and reproducible, and
    /// matches how the flake path already spells the same pin. A package with no recorded commit
    /// still falls back to the host's channel.
    /// </remarks>
    public static string DefaultNix(IReadOnlyList<string> packages, string? nixpkgsRev)
    {
        var pkgs = string.Join(" ", packages.Select(package => "pkgs." + package));
        var nixpkgs = string.IsNullOrWhiteSpace(nixpkgsRev)
            ? "import <nixpkgs> {}"
            : "import (builtins.fetchTarball {\n"
              + "    url = \"https://github.com/NixOS/nixpkgs/archive/" + nixpkgsRev.Trim() + ".tar.gz\";\n"
              + "  }) {}";
        var dotnetSdk = packages.FirstOrDefault(package => package.StartsWith("dotnet-sdk", StringComparison.Ordinal));
        var dotnetRoot = string.IsNullOrWhiteSpace(dotnetSdk)
            ? ""
            : $$"""

                export DOTNET_ROOT="${pkgs.{{dotnetSdk}}}/share/dotnet"
                export DOTNET_HOST_PATH="$DOTNET_ROOT/dotnet"
                unset MSBuildSDKsPath
                unset MSBUILD_EXE_PATH
                unset DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR
                unset DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR
                export DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1
                export DOTNET_MULTILEVEL_LOOKUP=0
            """;
        return $$"""
            { pkgs ? {{nixpkgs}} }:
            pkgs.mkShell {
              packages = [ {{pkgs}} ];
              shellHook = ''
                extraBin="${toString ./bin}"
                if [ -d "$extraBin" ]; then
                  export PATH="$extraBin:$PATH"
                fi
                if [ -n "''${AGENT_UP_DEV_ROOT-}" ]; then
                  export PATH="$AGENT_UP_DEV_ROOT/bin:$AGENT_UP_DEV_ROOT/npm/node_modules/.bin:$PATH"
                fi{{dotnetRoot}}
              '';
            }

            """;
    }
}
