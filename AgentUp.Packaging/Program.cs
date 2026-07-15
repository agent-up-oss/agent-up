using AgentUp.Packaging.Features.ReleaseArtifacts;
using AgentUp.Packaging.Features.MacOs;
using AgentUp.Packaging.Features.Ubuntu;
using AgentUp.Packaging.Features.Windows;

if (args.Length is < 4 or > 7 || args[0] != "package")
{
    Console.Error.WriteLine("Usage: AgentUp.Packaging package <platform> <runtime-id> <version> [output-dir] [--payload-root <path>]");
    return 2;
}

var platform = args[1];
var rid = args[2];
var version = args[3];
var outputDir = "artifacts";
var payloadRoot = Environment.GetEnvironmentVariable("AGENTUP_PACKAGE_PAYLOAD_ROOT");

var index = 4;
if (index < args.Length && args[index] != "--payload-root")
{
    outputDir = args[index];
    index++;
}

if (index < args.Length)
{
    if (index + 1 >= args.Length || args[index] != "--payload-root")
    {
        Console.Error.WriteLine("Usage: AgentUp.Packaging package <platform> <runtime-id> <version> [output-dir] [--payload-root <path>]");
        return 2;
    }

    payloadRoot = args[index + 1];
    index += 2;
}

if (index != args.Length)
{
    Console.Error.WriteLine("Usage: AgentUp.Packaging package <platform> <runtime-id> <version> [output-dir] [--payload-root <path>]");
    return 2;
}

var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Release";
var root = RepositoryPaths.FindRepositoryRoot(AppContext.BaseDirectory);

if (platform == "ubuntu")
{
    var request = new PackageRequest(root, platform, rid, version, outputDir, configuration, payloadRoot);
    var packager = new UbuntuPackager(new ProcessCommandRunner(), new FileSystemPackageWriter());
    await packager.PackageAsync(request);
    return 0;
}

if (platform == "windows")
{
    var request = new PackageRequest(root, platform, rid, version, outputDir, configuration, payloadRoot);
    var packager = new WindowsPackager(new ProcessCommandRunner(), new WindowsFileSystemPackageWriter());
    await packager.PackageAsync(request);
    return 0;
}

if (platform == "macos")
{
    var request = new PackageRequest(root, platform, rid, version, outputDir, configuration, payloadRoot);
    var packager = new MacOsPackager(new ProcessCommandRunner(), new MacOsFileSystemPackageWriter());
    await packager.PackageAsync(request);
    return 0;
}

Console.Error.WriteLine($"Platform '{platform}' is not yet implemented by AgentUp.Packaging.");
return 78;
