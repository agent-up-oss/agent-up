using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

namespace AgentUp.Capabilities.Common.Features.NixRuntime.Models;

public static class FirstPartyNixPin
{
    public const string Rev = "b134951a4c9f3c995fd7be05f9a8dafa8c4ffb90";
    public const string Sha256 = "sha256-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    public static CapabilityNixpkgsPin Nixpkgs { get; } = new() { Rev = Rev, Sha256 = Sha256 };

    public static string DefaultNix(IReadOnlyList<string> packages)
    {
        var pkgs = string.Join(" ", packages.Select(package => "pkgs." + package));
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
            { pkgs ? import <nixpkgs> {} }:
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
