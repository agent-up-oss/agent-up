# LocalInstaller.App

Avalonia-based installer GUI and command-line interface built on **LocalInstaller.Core**.

Provides `AppComposition` for creating the installer window and the CLI controller,
plus the `CapabilityArtifact` model used to describe bundled application components.

## Install

```bash
dotnet add package LocalInstaller.App
```

## Usage

Wire `LocalInstallerApp` into a standard Avalonia `Program.cs`:

```csharp
using AgentUp.InstallerApp.Composition;
using Acme.Studio.Cli;
using Acme.Studio.Desktop;
using Acme.Studio.Installer;
using Acme.Studio.Server;
using Acme.Studio.Tray;

return await LocalInstallerApp.Create(args)
    .UseProductManifest<AcmeStudioProductManifest>()
    .InstallerOptionCli<AcmeStudioCliManifest>()
    .InstallerOptionServer<AcmeStudioServerManifest>()
    .InstallerOptionDesktop<AcmeStudioDesktopManifest>()
    .InstallerOptionTray<AcmeStudioTrayManifest>()
    .RunAsync<App>();
```
