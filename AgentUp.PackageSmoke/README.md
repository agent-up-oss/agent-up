# LocalInstaller.Smoke

Smoke-test runner for validating freshly installed products on Windows, macOS, Ubuntu,
and NixOS. Checks that services are running, CLI shims resolve, tray agents autostart,
and package signatures are intact.

Provides `PackageSmokeServiceRegistry`, `SmokeProductManifest`, and
`InstalledServiceSmokeRequest`. Wire up in a `Program.cs` console app that CI invokes
after a test install.

## Install

```bash
dotnet add package LocalInstaller.Smoke
```

## Usage

```csharp
using AgentUp.PackageSmoke.Features.SmokeRuns.DTOs;
using AgentUp.PackageSmoke.Shared.Factories;

var product = new SmokeProductManifest(
    ServiceName: "acme-studio-server",
    CliShimName: "acme-studio",
    ArtifactBaseName: "acme-studio",
    DisplayName: "Acme Studio",
    InstallDirName: "Acme Studio",
    WorkspaceConfigFileName: "acme-studio.json");

var controller = PackageSmokeServiceRegistry.CreateSmokeCommandController(product);
return await controller.ExecuteAsync(args, Console.Out, Console.Error);
```

Consumers can pass the product manifest directly through the registry or override it
per run with `--product-manifest <path>`. Run with `--help` to see the command
reference.
