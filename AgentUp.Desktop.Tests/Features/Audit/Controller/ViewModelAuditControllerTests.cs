using AgentUp.Desktop.Features.Audit.Controllers;

namespace AgentUp.Desktop.Tests.Features.Audit.Controller;

[TestFixture]
public sealed class ViewModelAuditControllerTests
{
    [Test]
    public void Dispose_canBeCalledWithoutAttach()
    {
        using var http = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
        using var controller = new ViewModelAuditController(http);

        Assert.DoesNotThrow(controller.Dispose);
    }
}
