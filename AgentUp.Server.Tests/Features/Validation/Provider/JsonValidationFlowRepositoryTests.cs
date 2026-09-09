using AgentUp.Server.Features.Validation.DTOs;
using AgentUp.Server.Features.Validation.Repositories;
namespace AgentUp.Server.Tests.Features.Validation.Provider;
public sealed class JsonValidationFlowRepositoryTests
{
    [Test]
    public async Task Save_and_load_preserve_flow_definition()
    {
        var directory = Path.Join(Path.GetTempPath(), "agent-up-validation-tests", Guid.NewGuid().ToString("N"));
        try { var repository = new JsonValidationFlowRepository(Path.Join(directory, "flows.json")); var flow = new ValidationFlow("id", "ws", "app", "Name", "Description", "/", [], [new("step", "Open settings", ValidationAction.Click, new(Text: "Settings"))], DateTimeOffset.UtcNow); await repository.SaveAsync([flow]); var loaded = await repository.LoadAsync(); var saved = loaded.Single(); Assert.Multiple(() => { Assert.That(saved.Id, Is.EqualTo(flow.Id)); Assert.That(saved.InitialPath, Is.EqualTo("/")); Assert.That(saved.Steps.Single().Description, Is.EqualTo("Open settings")); }); }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
