# LocalInstaller.Packaging

Build pipeline for producing platform-specific installer packages: Windows MSI (via WiX),
macOS `.pkg`, and Ubuntu `.deb`.

Provides `PackagingServiceRegistry` and `PackageProductManifest`. Wire up in a
`Program.cs` console app that CI invokes to produce release artifacts.

## Install

```bash
dotnet add package LocalInstaller.Packaging
```

## Usage

```csharp
using AgentUp.Packaging.Shared.Factories;

return await new PackagingServiceRegistry(
        productName: "My Product",
        slug: "my-product",
        environmentPrefix: "MY_PRODUCT",
        manufacturer: "My Company",
        windowsUpgradeCode: "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx")
    .PackageCommands.ExecuteAsync(args);
```

The packaging commands accept `--platform`, `--runtime-id`, `--version`, and
`--output-directory` flags. Run with `--help` to see the full option reference.
