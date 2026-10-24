using AgentUp.CLI.Features.Capabilities.DTOs;

namespace AgentUp.CLI.Features.Capabilities.Services;

public sealed class CapabilitiesOutputService(TextWriter output)
{
    public int Write(CapabilitiesCommandResult result)
    {
        output.WriteLine(result.Message);
        return result.ExitCode;
    }

    public static string FormatList(IReadOnlyList<CapabilityModuleDto> modules)
    {
        if (modules.Count == 0)
            return "No capability modules in the Server registry.";

        var lines = new List<string>();
        foreach (var module in modules)
        {
            lines.Add(
                $"{module.Id}  {module.Version}  enabled={(module.Enabled ? "true" : "false")}  canRun={(module.CanRun ? "true" : "false")}");
            if (module.CanRun)
                continue;

            foreach (var message in module.Messages)
                lines.Add($"  {message}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
