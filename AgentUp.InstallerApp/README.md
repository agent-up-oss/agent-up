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

var commandLine = AppComposition.CreateCommandLineController();
if (commandLine.ShouldRunCommandLine(args))
    return await commandLine.RunAsync(args, Console.Out, Console.Error);

return AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .WithInterFont()
    .UseReactiveUI()
    .StartWithClassicDesktopLifetime(args);
```
