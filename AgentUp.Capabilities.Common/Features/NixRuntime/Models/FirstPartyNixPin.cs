using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

namespace AgentUp.Capabilities.Common.Features.NixRuntime.Models;

public static class FirstPartyNixPin
{
    /// <summary>The nixpkgs commit every first-party package resolves its tools from.</summary>
    /// <remarks>
    /// The head of the nixos-26.05 release branch, which carries `dotnet-sdk_10`, `docker` and
    /// `nodejs_22` - the three attributes the first-party packages name. It has to be a commit
    /// that really exists: the shell fetches this tarball now, where the value used to be
    /// decorative because `import &lt;nixpkgs&gt;` ignored it. The previous value was a mangled copy
    /// of the nixos-24.05 head, sharing its first 25 characters, and GitHub answered 404 for it.
    /// </remarks>
    public const string Rev = "6d663c0533ff269008fb84e45930151e37c99db9";

    // The commit is the pin. First-party packages record no sha256 rather than the placeholder
    // they used to ship to satisfy a check that nothing verified.
    public static CapabilityNixpkgsPin Nixpkgs { get; } = new() { Rev = Rev };

    /// <summary>
    /// Native libraries the .NET host resolves with <c>dlopen</c> by soname instead of through a
    /// linked RPATH, so nixpkgs cannot patch them into the binaries and the shell has to put them
    /// on the library search path itself.
    /// </summary>
    /// <remarks>
    /// Missing either one aborts the process at startup rather than failing one request.
    /// Globalization dies in <c>CultureInfo.CurrentCulture</c> pointing at
    /// aka.ms/dotnet-missing-libicu, and TLS dies with "No usable version of libssl was found" the
    /// first time a connection negotiates - which for the Example API is its first Postgres
    /// connection, before it has served anything. <c>openssl</c> lists its <c>bin</c> output
    /// first, so the expression names <c>.out</c> to reach the one that holds <c>lib</c>.
    /// </remarks>
    private static readonly string[] DotnetNativeLibraries = ["icu", "openssl.out"];

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
        var dotnetSdkPackage = packages.FirstOrDefault(package => package.StartsWith("dotnet-sdk", StringComparison.Ordinal));
        // A module that delivers the SDK delivers what the SDK's own runtime opens by hand.
        var shellPackages = string.IsNullOrWhiteSpace(dotnetSdkPackage)
            ? packages
            : [.. packages, .. DotnetNativeLibraries];
        var pkgs = string.Join(" ", shellPackages.Select(package => "pkgs." + package));
        var nixpkgs = string.IsNullOrWhiteSpace(nixpkgsRev)
            ? "import <nixpkgs> {}"
            : "import (builtins.fetchTarball {\n"
              + "    url = \"https://github.com/NixOS/nixpkgs/archive/" + nixpkgsRev.Trim() + ".tar.gz\";\n"
              + "  }) {}";
        var dotnetSdk = dotnetSdkPackage;
        var nativeLibraryPath = string.Join(":", DotnetNativeLibraries.Select(name => "${pkgs." + name + "}/lib"));
        var dotnetRoot = string.IsNullOrWhiteSpace(dotnetSdk)
            ? ""
            : $$"""

                export DOTNET_ROOT="${pkgs.{{dotnetSdk}}}/share/dotnet"
                export DOTNET_HOST_PATH="$DOTNET_ROOT/dotnet"
                export LD_LIBRARY_PATH="{{nativeLibraryPath}}''${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
                unset MSBuildSDKsPath
                unset MSBUILD_EXE_PATH
                unset DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR
                unset DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR
                export DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1
                export DOTNET_MULTILEVEL_LOOKUP=0
            """;
        return $$"""
            { pkgs ? {{nixpkgs}} }:
            # A capability shell runs tools, it does not compile C. mkShell would pull the whole
            # stdenv - gcc, binutils, isl, perl, gnumake, the autotools hooks - into every host
            # that enables a module, and on a cold runner that closure is downloaded before the
            # first application can start.
            pkgs.mkShellNoCC {
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
