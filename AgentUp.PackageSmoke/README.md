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
using AgentUp.PackageSmoke.Shared.Factories;

var controller = PackageSmokeServiceRegistry.CreateSmokeCommandController();
return await controller.ExecuteAsync(args, Console.Out, Console.Error);
```

The smoke commands accept `--platform`, `--runtime-id`, `--artifact-directory`,
`--service-name`, `--cli-shim`, and `--display-name` flags. Run with `--help` to
see the full option reference.
