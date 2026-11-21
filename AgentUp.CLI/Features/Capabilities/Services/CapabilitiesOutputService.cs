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
            return "The Server reports no capabilities.";

        var lines = new List<string>();
        foreach (var module in modules)
        {
            lines.Add(
                $"{module.Id}  {module.Version}  enabled={Flag(module.Enabled)}  available={Flag(module.Available)}  canRun={Flag(module.CanRun)}");
            if (module.CanRun)
                continue;

            foreach (var message in module.Messages)
                lines.Add($"  {message}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string Flag(bool value) => value ? "true" : "false";
}
