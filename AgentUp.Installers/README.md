# LocalInstaller.Core

Cross-platform installer library for Windows, macOS, Ubuntu, and NixOS.

Provides the `ProductManifest` description type, the `InstallerPlatformAdapterFactory`
composition root, and the `IInstallerPlatformAdapter` interface with platform adapters
for all four targets. Pair with **LocalInstaller.App** for the Avalonia GUI.

## Install

```bash
dotnet add package LocalInstaller.Core
```

## Usage

```csharp
using AgentUp.Installers.Composition;
using AgentUp.Installers.Features.Installation.Models;

var manifest = new ProductManifest("My Product", "my-product", "MY_PRODUCT")
{
    Components = [ProductComponent.Desktop, ProductComponent.Server, ProductComponent.Cli],
    WindowsUpgradeCode = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
};

var adapter = InstallerPlatformAdapterFactory.Create(
    manifest,
    AppContext.BaseDirectory,
    fakeInstaller: Environment.GetEnvironmentVariable(manifest.FakeInstallerVariable),
    useNixOsLookupOnlyMode: false);

var session = new InstallerSession(installRoot: manifest.DefaultInstallRoot());

await foreach (var progress in adapter.ExecuteInstallAsync(session))
    Console.WriteLine(progress.Message);
```
