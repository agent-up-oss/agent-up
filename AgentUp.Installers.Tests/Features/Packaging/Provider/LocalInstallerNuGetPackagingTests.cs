using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AgentUp.Installers.Tests.Features.Packaging.Provider;

[TestFixture]
public class LocalInstallerNuGetPackagingTests
{
    private static readonly string[] ExpectedPackageIds =
    [
        "LocalInstaller.Core",
        "LocalInstaller.App",
        "LocalInstaller.Smoke",
        "LocalInstaller.Packaging"
    ];

    private static readonly string[] DependentPackageIds =
    [
        "LocalInstaller.App",
        "LocalInstaller.Smoke",
        "LocalInstaller.Packaging"
    ];

    private string _solutionRoot = null!;
    private string _workDir = null!;
    private string _packDir1 = null!;
    private string _packDir2 = null!;

    [OneTimeSetUp]
    public void PackTwice()
    {
        _solutionRoot = FindSolutionRoot(TestContext.CurrentContext.TestDirectory);
        _workDir = Path.Join(Path.GetTempPath(), $"localinstaller-pkg-{Guid.NewGuid():N}");
        _packDir1 = Path.Join(_workDir, "pack1");
        _packDir2 = Path.Join(_workDir, "pack2");
        Directory.CreateDirectory(_packDir1);
        Directory.CreateDirectory(_packDir2);

        DotnetPack(_solutionRoot, _packDir1);
        DotnetPack(_solutionRoot, _packDir2);
    }

    [OneTimeTearDown]
    public void Cleanup()
    {
        if (Directory.Exists(_workDir))
            Directory.Delete(_workDir, recursive: true);
    }

    [Test]
    public void Pack_producesFourNuGetPackages()
    {
        var packages = Directory.GetFiles(_packDir1, "*.nupkg");
        Assert.That(packages, Has.Length.EqualTo(4),
            $"Expected 4 .nupkg files but got {packages.Length}:\n{string.Join("\n", packages.Select(Path.GetFileName))}");
    }

    [TestCaseSource(nameof(ExpectedPackageIds))]
    public void EachPackage_hasLocalInstallerPackageId(string packageId)
    {
        var id = ReadNuspecElement(_packDir1, packageId, "id");
        Assert.That(id, Is.EqualTo(packageId));
    }

    [TestCaseSource(nameof(ExpectedPackageIds))]
    public void EachPackage_hasValidSemanticVersion(string packageId)
    {
        var version = ReadNuspecElement(_packDir1, packageId, "version");
        Assert.That(Regex.IsMatch(version, @"^\d+\.\d+\.\d+"), Is.True,
            $"'{version}' is not a valid semantic version");
    }

    [TestCaseSource(nameof(ExpectedPackageIds))]
    public void EachPackage_hasApacheLicenseFile(string packageId)
    {
        var nuspec = ReadNuspec(_packDir1, packageId);
        var license = nuspec.Root!.Descendants().FirstOrDefault(e => e.Name.LocalName == "license");
        Assert.That(license, Is.Not.Null, $"{packageId} must have a <license> element");
        Assert.That(license!.Value, Is.EqualTo("LICENSE"));
        Assert.That(license.Attribute("type")?.Value, Is.EqualTo("file"));
    }

    [TestCaseSource(nameof(ExpectedPackageIds))]
    public void EachPackage_hasRepositoryUrl(string packageId)
    {
        var nuspec = ReadNuspec(_packDir1, packageId);
        var repo = nuspec.Root!.Descendants().FirstOrDefault(e => e.Name.LocalName == "repository");
        Assert.That(repo, Is.Not.Null, $"{packageId} must have a <repository> element");
        Assert.That(repo!.Attribute("url")?.Value, Is.Not.Null.And.Not.Empty,
            $"{packageId} repository url must be set");
    }

    [TestCaseSource(nameof(DependentPackageIds))]
    public void DependentPackage_listsLocalInstallerCoreAsNuGetDependency(string packageId)
    {
        var nuspec = ReadNuspec(_packDir1, packageId);
        var dependencyIds = nuspec.Root!.Descendants()
            .Where(e => e.Name.LocalName == "dependency")
            .Select(e => e.Attribute("id")?.Value)
            .ToList();

        Assert.That(dependencyIds, Does.Contain("LocalInstaller.Core"),
            $"{packageId} must declare LocalInstaller.Core as a NuGet package dependency so a consumer " +
            "installing this package automatically pulls in the core library");
    }

    [TestCaseSource(nameof(ExpectedPackageIds))]
    public void Package_containsNoAgentUpProductConfigurationFiles(string packageId)
    {
        var entries = GetPackageEntries(_packDir1, packageId);

        var productConfigFiles = entries.Where(e =>
            Path.GetFileName(e).Equals("logo.ico", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(e).Equals("app.manifest", StringComparison.OrdinalIgnoreCase) ||
            (Path.GetExtension(e).Equals(".json", StringComparison.OrdinalIgnoreCase) &&
             Path.GetFileName(e).StartsWith("AgentUp", StringComparison.Ordinal) &&
             !Path.GetFileName(e).EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase))
        ).ToList();

        Assert.That(productConfigFiles, Is.Empty,
            $"{packageId} must not contain Agent-Up product configuration files:\n" +
            string.Join("\n", productConfigFiles));
    }

    [TestCaseSource(nameof(ExpectedPackageIds))]
    public void Package_containsNoTestFixtures(string packageId)
    {
        var entries = GetPackageEntries(_packDir1, packageId);

        var testFiles = entries.Where(e =>
            e.Contains(".Tests.", StringComparison.Ordinal) ||
            e.Contains("/Tests/", StringComparison.Ordinal) ||
            Path.GetExtension(e).Equals(".trx", StringComparison.OrdinalIgnoreCase)
        ).ToList();

        Assert.That(testFiles, Is.Empty,
            $"{packageId} must not contain test fixtures:\n" +
            string.Join("\n", testFiles));
    }

    [TestCaseSource(nameof(ExpectedPackageIds))]
    public void Pack_runTwice_producesByteForByteIdenticalPackage(string packageId)
    {
        var file1 = Directory.GetFiles(_packDir1, $"{packageId}.*.nupkg").Single();
        var file2 = Directory.GetFiles(_packDir2, $"{packageId}.*.nupkg").Single();

        var bytes1 = File.ReadAllBytes(file1);
        var bytes2 = File.ReadAllBytes(file2);

        Assert.That(bytes1.SequenceEqual(bytes2), Is.True,
            $"{packageId}: two pack runs from the same source tree must produce byte-for-byte identical .nupkg files " +
            "(no embedded timestamp, machine name, or environment value)");
    }

    [Test]
    public void ConsumingProject_canRestoreAndBuildAgainstAllFourPackagesViaLocalNuGetSource()
    {
        var consumerDir = Path.Join(_workDir, "consumer");
        Directory.CreateDirectory(consumerDir);

        // nuget.config that points only at the local package output — no source-tree references
        File.WriteAllText(Path.Join(consumerDir, "nuget.config"),
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="local-packages" value="{_packDir1.Replace('\\', '/')}" />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
              </packageSources>
            </configuration>
            """);

        File.WriteAllText(Path.Join(consumerDir, "Consumer.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="LocalInstaller.Core"      Version="1.0.0" />
                <PackageReference Include="LocalInstaller.App"       Version="1.0.0" />
                <PackageReference Include="LocalInstaller.Smoke"     Version="1.0.0" />
                <PackageReference Include="LocalInstaller.Packaging" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);

        // Each using / typeof() proves the type is accessible from the packed assembly
        File.WriteAllText(Path.Join(consumerDir, "Program.cs"),
            """
            using AgentUp.Installers.Features.Installation.Models;
            using AgentUp.InstallerApp.Features.Capabilities.Models;
            using AgentUp.PackageSmoke.Features.SmokeRuns.DTOs;
            using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

            _ = typeof(ProductManifest);       // LocalInstaller.Core
            _ = typeof(CapabilityArtifact);    // LocalInstaller.App
            _ = typeof(SmokeProductManifest);  // LocalInstaller.Smoke
            _ = typeof(PackageProductManifest);// LocalInstaller.Packaging
            """);

        var restore = RunDotnet("restore", consumerDir);
        Assert.That(restore.ExitCode, Is.EqualTo(0),
            $"dotnet restore failed — package graph is not self-contained:\n{restore.Output}");

        var build = RunDotnet("build --no-restore", consumerDir);
        Assert.That(build.ExitCode, Is.EqualTo(0),
            $"dotnet build failed — could not compile against the packed public API:\n{build.Output}");
    }

    // === helpers ===

    private static string FindSolutionRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            if (File.Exists(Path.Join(dir.FullName, "localinstaller.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException($"Could not find localinstaller.sln walking up from {startDir}");
    }

    private static void DotnetPack(string solutionRoot, string outputDir)
    {
        string[] projects =
        [
            Path.Join("AgentUp.Installers", "AgentUp.Installers.csproj"),
            Path.Join("AgentUp.InstallerApp", "AgentUp.InstallerApp.csproj"),
            Path.Join("AgentUp.PackageSmoke", "AgentUp.PackageSmoke.csproj"),
            Path.Join("AgentUp.Packaging", "AgentUp.Packaging.csproj"),
        ];

        foreach (var project in projects)
        {
            var result = RunDotnet(
                $"pack \"{Path.Join(solutionRoot, project)}\" --configuration Release --output \"{outputDir}\"",
                solutionRoot);

            if (result.ExitCode != 0)
                throw new InvalidOperationException($"dotnet pack {project} failed:\n{result.Output}");
        }
    }

    private static XDocument ReadNuspec(string packDir, string packageId)
    {
        var path = FindNupkg(packDir, packageId);
        using var zip = ZipFile.OpenRead(path);
        var entry = zip.Entries.First(e => e.Name.EndsWith(".nuspec", StringComparison.Ordinal));
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static string ReadNuspecElement(string packDir, string packageId, string elementName)
    {
        var nuspec = ReadNuspec(packDir, packageId);
        return nuspec.Root!.Descendants()
            .First(e => e.Name.LocalName.Equals(elementName, StringComparison.Ordinal))
            .Value;
    }

    private static IReadOnlyList<string> GetPackageEntries(string packDir, string packageId)
    {
        var path = FindNupkg(packDir, packageId);
        using var zip = ZipFile.OpenRead(path);
        return zip.Entries.Select(e => e.FullName).ToList();
    }

    private static string FindNupkg(string packDir, string packageId)
        => Directory.GetFiles(packDir, $"{packageId}.*.nupkg").SingleOrDefault()
           ?? throw new FileNotFoundException($"Package {packageId}.*.nupkg not found in {packDir}");

    private static (int ExitCode, string Output) RunDotnet(string args, string workingDirectory)
    {
        var psi = new ProcessStartInfo("dotnet", args)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi)!;
        var stdoutTask = Task.Run(() => process.StandardOutput.ReadToEnd());
        var stderrTask = Task.Run(() => process.StandardError.ReadToEnd());
        if (!process.WaitForExit(300_000))
        {
            process.Kill(entireProcessTree: true);
        }
        return (process.ExitCode, stdoutTask.GetAwaiter().GetResult() + stderrTask.GetAwaiter().GetResult());
    }
}
