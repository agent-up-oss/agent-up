using AgentUp.Desktop.Features.Database.DTOs;

namespace AgentUp.Desktop.Tests.Features.Database.Unit;

[TestFixture]
public sealed class DatabaseTableDtoTests
{
    [Test]
    public void DisplayName_includesSchema()
        => Assert.That(new DatabaseTableDto("public", "orders").DisplayName, Is.EqualTo("public.orders"));
}
