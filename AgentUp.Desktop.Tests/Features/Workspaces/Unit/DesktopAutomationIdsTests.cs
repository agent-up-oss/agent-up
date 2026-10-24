using AgentUp.Desktop.Shared.Providers;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Unit;

public sealed class DesktopAutomationIdsTests
{
    [Test]
    public void LoopSurface_usesStableCatalogIds() =>
        Assert.That(
            new[]
            {
                DesktopAutomationIds.AgentLoop,
                DesktopAutomationIds.AgentLoopChecklist,
                DesktopAutomationIds.AgentLoopEnable,
                DesktopAutomationIds.AgentLoopActivate,
                DesktopAutomationIds.AgentLoopCancel,
                DesktopAutomationIds.AgentLoopTerminal,
                DesktopAutomationIds.AgentLoopPhase,
                DesktopAutomationIds.AgentLoopPrecondition("hostConnectionAuthorized")
            },
            Is.EqualTo(new[]
            {
                "agent.loop",
                "agent.loop.checklist",
                "agent.loop.enable",
                "agent.loop.activate",
                "agent.loop.cancel",
                "agent.loop.terminal",
                "agent.loop.phase",
                "agent.loop.precondition.hostConnectionAuthorized"
            }));

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
                DesktopAutomationIds.AgentLoopPrecondition("autonomyEnabled"),
                DesktopAutomationIds.AgentPickerRow("Codex"),
                DesktopAutomationIds.AgentEnable("Codex"),
                DesktopAutomationIds.AgentThought("think-1"),
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
                DesktopAutomationIds.AuditPage(2),
                DesktopAutomationIds.AuditFilter("error"),
                DesktopAutomationIds.GitLogRef("main"),
                DesktopAutomationIds.GitDiffJump("@@ -1 +1 @@"),
                DesktopAutomationIds.ValidationPlay("flow"),
                DesktopAutomationIds.CapabilityEnable("dotnet"),
                DesktopAutomationIds.CapabilityDisable("dotnet"),
                DesktopAutomationIds.ConsoleShowMore,
                DesktopAutomationIds.CapabilityClose
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
                "agent.loop.precondition.autonomyEnabled",
                "agent.picker.Codex",
                "agent.enable.Codex",
                "agent.thought.think-1",
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
                "audit.page.2",
                "audit.filter.error",
                "git.log.ref.main",
                "git.diff.jump.@@ -1 +1 @@",
                "validation.play.flow",
                "capability.enable.dotnet",
                "capability.disable.dotnet",
                "console.show-more",
                "capability.close"
            }));
}
