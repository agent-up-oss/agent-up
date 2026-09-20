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
        return $$"""
            { pkgs ? import (fetchTarball {
              url = "https://github.com/NixOS/nixpkgs/archive/{{Rev}}.tar.gz";
              sha256 = "{{Sha256}}";
            }) {} }:
            pkgs.mkShell {
              packages = [ {{pkgs}} ];
              shellHook = ''
                extraBin="${toString ./bin}"
                if [ -d "$extraBin" ]; then
                  export PATH="$extraBin:$PATH"
                fi
                if [ -n "${AGENT_UP_DEV_ROOT-}" ]; then
                  export PATH="$AGENT_UP_DEV_ROOT/bin:$AGENT_UP_DEV_ROOT/npm/node_modules/.bin:$PATH"
                fi
              '';
            }

            """;
    }
}
