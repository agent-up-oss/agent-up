using AgentUp.Desktop.Features.Audit.Services;

namespace AgentUp.Desktop.Tests.Features.Audit.Unit;

[TestFixture]
public sealed class ViewModelAuditorTests
{
    [Test]
    public void Dispose_isIdempotent()
    {
        using var http = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
        var auditor = new ViewModelAuditor(http);

        auditor.Dispose();

        Assert.DoesNotThrow(auditor.Dispose);
    }
}
