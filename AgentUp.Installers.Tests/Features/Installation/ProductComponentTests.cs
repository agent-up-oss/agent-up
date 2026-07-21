using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation.Providers;

namespace AgentUp.Installers.Tests.Features.Installation;

[TestFixture]
public class ProductComponentTests
{
    private static ProductManifest EditorRenderer => new("Acme Studio", "acme-studio", "ACMESTUDIO")
    {
        Components =
        [
            new ProductComponent("editor", "Editor", "Visual editing surface."),
            new ProductComponent("renderer", "Renderer", "Output renderer.")
        ]
    };

    [Test]
    public async Task FakeAdapter_forTwoComponentProduct_installsEachComponentIndependently_withNoAgentUpNames()
    {
        var manifest = EditorRenderer;
        var session = InstallerSession.CreateDefault(
            manifest, new Version(1, 0, 0), "/opt/acme-studio",
            PayloadSelection.Bundled("Acme Studio", new Version(1, 0, 0)));
        var adapter = new FakeInstallerPlatformAdapter();

        var editor = manifest.Components[0];
        var renderer = manifest.Components[1];

        // Install editor
        await foreach (var progress in adapter.ExecuteComponentActionAsync(editor, InstallerComponentAction.Install, session)) { _ = progress; }
        var editorStatus = await adapter.GetComponentStatusAsync(editor, session);
        var rendererStatus = await adapter.GetComponentStatusAsync(renderer, session);

        Assert.That(editorStatus.Kind, Is.EqualTo(InstallerComponentStatusKind.Installed));
        Assert.That(rendererStatus.Kind, Is.EqualTo(InstallerComponentStatusKind.NotInstalled));

        // Uninstall editor, then repair renderer
        await foreach (var progress in adapter.ExecuteComponentActionAsync(editor, InstallerComponentAction.Uninstall, session)) { _ = progress; }
        await foreach (var progress in adapter.ExecuteComponentActionAsync(renderer, InstallerComponentAction.Repair, session)) { _ = progress; }

        // Collect all plan titles and progress messages
        var allTitles = new List<string>();
        foreach (var component in manifest.Components)
        {
            var plan = adapter.PlanComponentAction(component, InstallerComponentAction.Install, session);
            allTitles.AddRange(plan.Select(op => op.Title));
        }

        var progressMessages = new List<string>();
        var adapter2 = new FakeInstallerPlatformAdapter();
        foreach (var component in manifest.Components)
        {
            await foreach (var p in adapter2.ExecuteComponentActionAsync(component, InstallerComponentAction.Install, session))
                progressMessages.Add(p.Message);
        }

        var allText = string.Join(" ", allTitles.Concat(progressMessages));

        Assert.Multiple(() =>
        {
            Assert.That(allText, Does.Not.Contain("Desktop"), "No 'Desktop' should appear in an Editor/Renderer session");
            Assert.That(allText, Does.Not.Contain("Server"), "No 'Server' should appear in an Editor/Renderer session");
            Assert.That(allText, Does.Not.Contain("CLI"), "No 'CLI' should appear in an Editor/Renderer session");
            Assert.That(allText, Does.Not.Contain("Agent-Up"), "No 'Agent-Up' should appear in an Editor/Renderer session");
            Assert.That(allText, Does.Contain("Editor").Or.Contain("Renderer"), "Session text must reference Editor or Renderer components");
        });
    }

    [Test]
    public async Task FakeAdapter_forComponentNotInManifest_throwsBeforeAnyProgressEvent()
    {
        var session = InstallerSession.CreateDefault(
            ProductManifest.AgentUp(), new Version(1, 0, 0), "/opt/agent-up",
            PayloadSelection.Bundled(new Version(1, 0, 0)));
        var adapter = new FakeInstallerPlatformAdapter();
        var unknownComponent = new ProductComponent("unknown", "Unknown");

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in adapter.ExecuteComponentActionAsync(unknownComponent, InstallerComponentAction.Install, session))
            {
                Assert.Fail("No progress events should be yielded before the exception.");
            }
        });

        Assert.That(ex!.Message, Does.Contain("unknown").Or.Contain("Unknown"));
        Assert.That(ex.Message, Does.Contain("Agent-Up"));
    }

    [Test]
    public async Task FakeAdapter_agentUpBaseline_allThreeComponentsInstallableAndNamed()
    {
        var session = InstallerSession.CreateDefault(
            ProductManifest.AgentUp(), new Version(1, 0, 0), "/opt/agent-up",
            PayloadSelection.Bundled(new Version(1, 0, 0)));
        var adapter = new FakeInstallerPlatformAdapter();

        foreach (var component in session.Manifest.Components)
        {
            await foreach (var progress in adapter.ExecuteComponentActionAsync(component, InstallerComponentAction.Install, session)) { _ = progress; }
            var status = await adapter.GetComponentStatusAsync(component, session);
            var plan = adapter.PlanComponentAction(component, InstallerComponentAction.Install, session);

            Assert.That(status.Kind, Is.EqualTo(InstallerComponentStatusKind.Installed), $"{component.DisplayName} should be Installed");
            Assert.That(plan.Any(op => op.Title.Contains(component.DisplayName, StringComparison.OrdinalIgnoreCase)),
                Is.True, $"Plan for {component.DisplayName} must contain the component's display name in at least one operation title");
        }

        Assert.That(session.Manifest.Components, Has.Count.EqualTo(3));
    }

    private static IEnumerable<TestCaseData> ManifestCases()
    {
        yield return new TestCaseData(ProductManifest.AgentUp()).SetName("AgentUp_threeComponents");
        yield return new TestCaseData(EditorRenderer).SetName("EditorRenderer_twoComponents");
    }

    [TestCaseSource(nameof(ManifestCases))]
    public async Task FakeAdapter_forAnyManifest_installsEachComponentAndProducesNamedFourStepPlan(ProductManifest manifest)
    {
        var session = InstallerSession.CreateDefault(
            manifest, new Version(1, 0, 0), "/opt/test",
            PayloadSelection.Bundled(manifest.ProductName, new Version(1, 0, 0)));
        var adapter = new FakeInstallerPlatformAdapter();

        foreach (var component in manifest.Components)
        {
            var plan = adapter.PlanComponentAction(component, InstallerComponentAction.Install, session);
            Assert.That(plan, Has.Count.EqualTo(4), $"Component '{component.DisplayName}' must produce a 4-step plan");
            Assert.That(plan.Any(op => op.Title.Contains(component.DisplayName, StringComparison.OrdinalIgnoreCase)),
                Is.True, $"Plan for '{component.DisplayName}' must mention the component's display name");

            var otherComponentNames = manifest.Components
                .Where(c => c.Id != component.Id)
                .Select(c => c.DisplayName)
                .ToList();
            foreach (var otherName in otherComponentNames)
                Assert.That(plan.All(op => !op.Title.Contains(otherName, StringComparison.OrdinalIgnoreCase)),
                    Is.True, $"Plan for '{component.DisplayName}' must not mention '{otherName}'");

            var progressMessages = new List<string>();
            await foreach (var p in adapter.ExecuteComponentActionAsync(component, InstallerComponentAction.Install, session))
                progressMessages.Add(p.Message);

            Assert.That(progressMessages, Has.Count.EqualTo(plan.Count));

            var status = await adapter.GetComponentStatusAsync(component, session);
            Assert.That(status.Kind, Is.EqualTo(InstallerComponentStatusKind.Installed), $"'{component.DisplayName}' must be Installed after install");
        }
    }
}
