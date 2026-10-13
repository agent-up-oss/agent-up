using AgentUp.Server.Features.Agents.Providers;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class RoleToolFilterProviderTests
{
    [Test]
    public void Restrict_leavesTheToolsetWhenTheAllowlistIsEmpty()
    {
        var options = new McpServerOptions();
        var before = options.Filters.Request.CallToolFilters.Count;

        new RoleToolFilterProvider().Restrict(options, []);

        Assert.That(options.Filters.Request.CallToolFilters.Count, Is.EqualTo(before));
    }

    [Test]
    public void Restrict_installsACallFilterWhenTheAllowlistIsNamed()
    {
        var options = new McpServerOptions();
        var before = options.Filters.Request.CallToolFilters.Count;

        new RoleToolFilterProvider().Restrict(options, ["read_file"]);

        Assert.That(options.Filters.Request.CallToolFilters.Count, Is.EqualTo(before + 1));
    }
}
