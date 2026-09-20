using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Models;

namespace AgentUp.Capabilities.Common.Tests.Features.CapabilityDiscovery.Unit;

[TestFixture]
public sealed class CapabilityCommandResultTests
{
    [Test]
    public void Result_preserves_exit_code_and_both_output_streams()
    {
        var result = new CapabilityCommandResult(17, "version 1.2.3", "warning");

        Assert.That((result.ExitCode, result.Stdout, result.Stderr),
            Is.EqualTo((17, "version 1.2.3", "warning")));
    }

    [Test]
    public void Record_equality_compares_command_results_by_value()
        => Assert.That(
            new CapabilityCommandResult(0, "ok", string.Empty),
            Is.EqualTo(new CapabilityCommandResult(0, "ok", string.Empty)));
}
