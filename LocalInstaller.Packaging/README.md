# LocalInstaller.Packaging

Build pipeline for producing platform-specific installer packages: Windows MSI (via WiX),
macOS `.pkg`, and Ubuntu `.deb`.

Provides `LocalInstallerPackager` and `PackageProductManifest`. Wire up in a
`Program.cs` console app that CI invokes to produce release artifacts.

## Install

```bash
dotnet add package LocalInstaller.Packaging
```

## Usage

```csharp
using AgentUp.Packaging.Composition;
using Acme.Studio.Cli;
using Acme.Studio.Desktop;
using Acme.Studio.Installer;
using Acme.Studio.Server;
using Acme.Studio.Tray;

return await LocalInstallerPackager.Create(args)
    .UseProductManifest<AcmeStudioProductManifest>()
    .InstallerApplication<AcmeStudioInstallerAppManifest>()
    .InstallerOptionCli<AcmeStudioCliManifest>()
    .InstallerOptionServer<AcmeStudioServerManifest>()
    .InstallerOptionDesktop<AcmeStudioDesktopManifest>()
    .InstallerOptionTray<AcmeStudioTrayManifest>()
    .Windows(options => options.WithUpgradeCode("xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"))
    .RunAsync();
```

The packaging commands accept `--platform`, `--runtime-id`, `--version`, and
`--output-directory` flags. Run with `--help` to see the full option reference.
