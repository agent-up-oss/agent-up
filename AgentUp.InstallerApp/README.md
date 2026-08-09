# LocalInstaller.App

Avalonia-based installer GUI and command-line interface built on **LocalInstaller.Core**.

Provides `AppComposition` for creating the installer window and the CLI controller,
plus the `CapabilityArtifact` model used to describe bundled application components.

## Install

```bash
dotnet add package LocalInstaller.App
```

## Usage

Wire `AppComposition` into a standard Avalonia `Program.cs`:

```csharp
using Avalonia;
using Avalonia.ReactiveUI;
using AgentUp.InstallerApp;
using AgentUp.InstallerApp.Composition;
using AgentUp.Installers.Composition;
using AgentUp.Installers.Features.Installation.Models;

var product = new ProductManifest("Acme Studio", "acme-studio", "ACMESTUDIO")
{
    Components = [ProductComponent.Cli, ProductComponent.Server, ProductComponent.Desktop]
};

AppComposition.ConfigureProduct(product, "ACMESTUDIO_INSTALLER_FAKE");

var commandLine = AppComposition.CreateCommandLineController();
if (commandLine.ShouldRunCommandLine(args))
{
    var adapter = InstallerPlatformAdapterFactory.Create(
        product,
        AppContext.BaseDirectory,
        Environment.GetEnvironmentVariable("ACMESTUDIO_INSTALLER_FAKE"),
        useNixOsLookupOnlyMode: false);
    return await commandLine.RunAsync(adapter, product, args, Console.Out, Console.Error);
}

return AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .WithInterFont()
    .UseReactiveUI()
    .StartWithClassicDesktopLifetime(args);
```
