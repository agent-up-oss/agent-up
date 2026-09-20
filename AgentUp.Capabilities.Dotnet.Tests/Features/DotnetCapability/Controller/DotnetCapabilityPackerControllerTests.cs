using AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Controllers;
using AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Providers;
using AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Services;

namespace AgentUp.Capabilities.Dotnet.Tests.Features.DotnetCapability.Controller;

[TestFixture]
public sealed class DotnetCapabilityPackerControllerTests
{
    [Test]
    public void Pack_writes_the_package_directory()
    {
        var directory = CreateDirectory();
        try
        {
            var path = new DotnetCapabilityPackerController(
                new DotnetCapabilityPackerService(new DotnetCapabilityPacker(), new DotnetCapabilityPackageWriter()))
                .Pack(directory);

            Assert.That(path, Is.EqualTo(directory));
            Assert.That(File.Exists(Path.Join(directory, "capability.json")), Is.True);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void Pack_writes_default_nix()
    {
        var directory = CreateDirectory();
        try
        {
            new DotnetCapabilityPackerController(
                new DotnetCapabilityPackerService(new DotnetCapabilityPacker(), new DotnetCapabilityPackageWriter()))
                .Pack(directory);

            Assert.That(File.Exists(Path.Join(directory, "default.nix")), Is.True);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateDirectory()
        => Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "agent-up-dotnet-ctrl-" + Guid.NewGuid().ToString("N"))).FullName;
}
