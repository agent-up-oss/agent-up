using AgentUp.InstallerApp.Features.Installation.Services;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation.Providers;

namespace AgentUp.InstallerApp.Tests.Features.Installation.TerminalIntegration;

[TestFixture]
public class InstallerCommandLineTests
{
    [Test]
    public void ShouldRunCommandLine_detectsInstallerCommandArguments()
    {
        Assert.That(InstallerCommandLine.ShouldRunCommandLine(["--payload-root", "/payload", "--install-core"]), Is.True);
        Assert.That(InstallerCommandLine.ShouldRunCommandLine(["--smoke-installer-operations"]), Is.True);
        Assert.That(InstallerCommandLine.ShouldRunCommandLine(["--install-component", "cli"]), Is.True);
        Assert.That(InstallerCommandLine.ShouldRunCommandLine(["--payload-root", "/payload"]), Is.False);
    }

    [Test]
    public async Task RunAsync_installCore_executesAdapterAndReportsSuccess()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await InstallerCommandLine.RunAsync(
            new FakeInstallerPlatformAdapter("Test"),
            ["--install-core"],
            output,
            error);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(output.ToString(), Does.Contain("Core app installation succeeded."));
        Assert.That(error.ToString(), Is.Empty);
    }

    [Test]
    public async Task RunAsync_validateInstalled_reportsSuccess()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await InstallerCommandLine.RunAsync(
            new FakeInstallerPlatformAdapter("Test"),
            ["--validate-installed"],
            output,
            error);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(output.ToString(), Does.Contain("Installed state validation succeeded."));
        Assert.That(error.ToString(), Is.Empty);
    }

    [Test]
    public async Task RunAsync_componentActionsAssertPostActionStatus()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var adapter = new FakeInstallerPlatformAdapter("Test");

        var install = await InstallerCommandLine.RunAsync(adapter, ["--install-component", "cli"], output, error);
        var repair = await InstallerCommandLine.RunAsync(adapter, ["--repair-component", "cli"], output, error);
        var update = await InstallerCommandLine.RunAsync(adapter, ["--update-component", "cli"], output, error);
        var uninstall = await InstallerCommandLine.RunAsync(adapter, ["--uninstall-component", "cli"], output, error);

        Assert.That(new[] { install, repair, update, uninstall }, Is.All.EqualTo(0));
        Assert.That(output.ToString(), Does.Contain("CLI Install succeeded."));
        Assert.That(output.ToString(), Does.Contain("CLI Repair succeeded."));
        Assert.That(output.ToString(), Does.Contain("CLI Update succeeded."));
        Assert.That(output.ToString(), Does.Contain("CLI Uninstall succeeded."));
        Assert.That(error.ToString(), Is.Empty);
    }

    [Test]
    public async Task RunAsync_smokeInstallerOperationsCoversCoreValidateAndComponentLifecycle()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await InstallerCommandLine.RunAsync(
            new FakeInstallerPlatformAdapter("Test"),
            ["--smoke-installer-operations"],
            output,
            error);

        Assert.That(exitCode, Is.EqualTo(0));
        var log = output.ToString();
        Assert.That(log, Does.Contain("Desktop Install succeeded."));
        Assert.That(log, Does.Contain("Desktop Uninstall succeeded."));
        Assert.That(log, Does.Contain("Server Install succeeded."));
        Assert.That(log, Does.Contain("Server Uninstall succeeded."));
        Assert.That(log, Does.Contain("CLI Install succeeded."));
        Assert.That(log, Does.Contain("CLI Uninstall succeeded."));
        Assert.That(log.IndexOf("Desktop Install succeeded.", StringComparison.Ordinal), Is.LessThan(log.IndexOf("Core app installation succeeded.", StringComparison.Ordinal)));
        Assert.That(log, Does.Contain("Core app installation succeeded."));
        Assert.That(log, Does.Contain("Installed state validation succeeded."));
        Assert.That(log, Does.Contain("Installer operation smoke succeeded."));
        Assert.That(error.ToString(), Is.Empty);
    }

    [Test]
    public async Task RunAsync_installComponent_acceptsOnlyDeclaredProductComponentIds()
    {
        var manifest = new ProductManifest("Acme Studio", "acme-studio", "ACMESTUDIO")
        {
            Components =
            [
                new ProductComponent("editor", "Editor", "Visual editing surface."),
                new ProductComponent("renderer", "Renderer", "Output renderer.")
            ]
        };
        var adapter = new FakeInstallerPlatformAdapter("Test");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await InstallerCommandLine.RunAsync(adapter, manifest, ["--install-component", "editor"], output, error);
        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(output.ToString(), Does.Contain("Editor Install succeeded."));

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => { _ = await InstallerCommandLine.RunAsync(adapter, manifest, ["--install-component", "desktop"], output, error); });
        Assert.ThrowsAsync<InvalidOperationException>(
            async () => { _ = await InstallerCommandLine.RunAsync(adapter, manifest, ["--install-component", "server"], output, error); });
        Assert.ThrowsAsync<InvalidOperationException>(
            async () => { _ = await InstallerCommandLine.RunAsync(adapter, manifest, ["--install-component", "cli"], output, error); });
    }

    [Test]
    public async Task RunAsync_installComponent_throws_whenTargetIsMissing()
    {
        var manifest = new ProductManifest("Acme Studio", "acme-studio", "ACMESTUDIO")
        {
            Components =
            [
                new ProductComponent("editor", "Editor", "Visual editing surface."),
                new ProductComponent("renderer", "Renderer", "Output renderer.")
            ]
        };
        var adapter = new FakeInstallerPlatformAdapter("Test");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            async () => { _ = await InstallerCommandLine.RunAsync(adapter, manifest, ["--install-component"], output, error); });

        Assert.That(ex!.Message, Does.Contain("--install-component"));
        Assert.That(ex.Message, Does.Contain("requires a component target"));
    }

    [Test]
    public async Task RunAsync_installComponent_throws_whenTargetIsWhitespaceOnly()
    {
        var manifest = new ProductManifest("Acme Studio", "acme-studio", "ACMESTUDIO")
        {
            Components =
            [
                new ProductComponent("editor", "Editor", "Visual editing surface."),
                new ProductComponent("renderer", "Renderer", "Output renderer.")
            ]
        };
        var adapter = new FakeInstallerPlatformAdapter("Test");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            async () => { _ = await InstallerCommandLine.RunAsync(adapter, manifest, ["--install-component", "   "], output, error); });

        Assert.That(ex!.Message, Does.Contain("--install-component"));
        Assert.That(ex.Message, Does.Contain("requires a component target"));
    }
}
