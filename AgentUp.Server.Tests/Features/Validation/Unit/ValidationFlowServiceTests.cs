using AgentUp.Server.Features.Validation.DTOs;
using AgentUp.Server.Features.Validation.Services;
namespace AgentUp.Server.Tests.Features.Validation.Unit;
public sealed class ValidationFlowServiceTests
{
    [Test]
    public void Service_contract_supports_edit_replay_export_and_delete()
    {
        var methods = typeof(ValidationFlowService).GetMethods().Select(x => x.Name).ToArray();
        Assert.Multiple(() => { Assert.That(methods, Does.Contain(nameof(ValidationFlowService.SaveAsync))); Assert.That(methods, Does.Contain(nameof(ValidationFlowService.RunAsync))); Assert.That(methods, Does.Contain(nameof(ValidationFlowService.ExportAsync))); Assert.That(methods, Does.Contain(nameof(ValidationFlowService.DeleteAsync))); Assert.That(typeof(ValidationFlow).GetProperty(nameof(ValidationFlow.Version)), Is.Not.Null); });
    }
}
