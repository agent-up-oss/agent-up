# LocalInstaller.Smoke

Smoke-test runner for validating freshly installed products on Windows, macOS, Ubuntu,
and NixOS. Checks that services are running, CLI shims resolve, tray agents autostart,
and package signatures are intact.

Provides `LocalInstallerSmoke`, `SmokeProductManifest`, and
`InstalledServiceSmokeRequest`. Wire up in a `Program.cs` console app that CI invokes
after a test install.

## Install

```bash
dotnet add package LocalInstaller.Smoke
```

## Usage

```csharp
using AgentUp.PackageSmoke.Composition;
using Acme.Studio.Cli;
using Acme.Studio.Desktop;
using Acme.Studio.Installer;
using Acme.Studio.Server;
using Acme.Studio.Tray;

return await LocalInstallerSmoke.Create(args)
    .UseProductManifest<AcmeStudioProductManifest>()
    .InstallerApplication<AcmeStudioInstallerAppManifest>()
    .InstallerOptionCli<AcmeStudioCliManifest>()
    .InstallerOptionServer<AcmeStudioServerManifest>()
    .InstallerOptionDesktop<AcmeStudioDesktopManifest>()
    .InstallerOptionTray<AcmeStudioTrayManifest>()
    .WorkspaceConfigFileName("acme-studio.json")
    .RunAsync(Console.Out, Console.Error);
```

Consumers can pass the product manifest directly through the registry or override it
per run with `--product-manifest <path>`. Run with `--help` to see the command
reference.
