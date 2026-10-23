using AgentUp.Desktop.Shared.Providers;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Unit;

public sealed class DesktopAutomationIdsTests
{
    [Test]
    public void Formatters_prefixDynamicIdsWithTheStableCatalogStems() =>
        Assert.That(
            new[]
            {
                DesktopAutomationIds.LoginSaved("srv"),
                DesktopAutomationIds.LoginRemoveSaved("srv"),
                DesktopAutomationIds.WorkspaceStart("ws"),
                DesktopAutomationIds.WorkspaceStop("ws"),
                DesktopAutomationIds.WorkspaceRefresh("ws"),
                DesktopAutomationIds.WorkspaceRemove("ws"),
                DesktopAutomationIds.ApplicationTab("Web"),
                DesktopAutomationIds.AgentPickerRow("Codex"),
                DesktopAutomationIds.AgentSession("s1"),
                DesktopAutomationIds.AgentOption("allow-once"),
                DesktopAutomationIds.GitSelect("src/App.cs"),
                DesktopAutomationIds.GitToggle("src/App.cs"),
                DesktopAutomationIds.GitOpen("src/App.cs"),
                DesktopAutomationIds.GitHosting,
                DesktopAutomationIds.GitHostingTitle,
                DesktopAutomationIds.GitHostingRemedy,
                DesktopAutomationIds.GitHostingSignIn,
                DesktopAutomationIds.GitHostingOpenSignIn,
                DesktopAutomationIds.GitHostingChecks,
                DesktopAutomationIds.ValidationPlay("flow"),
                DesktopAutomationIds.CapabilityEnable("dotnet"),
                DesktopAutomationIds.CapabilityDisable("dotnet")
            },
            Is.EqualTo(new[]
            {
                "login.saved.srv",
                "login.remove.srv",
                "workspace.start.ws",
                "workspace.stop.ws",
                "workspace.refresh.ws",
                "workspace.remove.ws",
                "app.tab.Web",
                "agent.picker.Codex",
                "agent.session.s1",
                "agent.option.allow-once",
                "git.select.src/App.cs",
                "git.toggle.src/App.cs",
                "git.open.src/App.cs",
                "git.hosting",
                "git.hosting.title",
                "git.hosting.remedy",
                "git.hosting.sign-in",
                "git.hosting.open-sign-in",
                "git.hosting.checks",
                "validation.play.flow",
                "capability.enable.dotnet",
                "capability.disable.dotnet"
            }));
}
