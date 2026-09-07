using AgentUp.Server.Features.Database.Models;

namespace AgentUp.Server.Tests.Features.Database.Unit;

[TestFixture]
public class DatabaseExplorerResultTests
{
    [Test]
    public void Success_StoresValue()
    {
        var result = DatabaseExplorerResult<string>.Success("inventory");

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(DatabaseExplorerStatus.Success));
            Assert.That(result.Value, Is.EqualTo("inventory"));
        });
    }
}
