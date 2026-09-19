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

    [Test]
    public void Dispose_is_idempotent_when_no_view_model_was_attached()
    {
        using var http = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
        var controller = new ViewModelAuditController(http);

        controller.Dispose();

        Assert.DoesNotThrow(controller.Dispose);
    }
}
