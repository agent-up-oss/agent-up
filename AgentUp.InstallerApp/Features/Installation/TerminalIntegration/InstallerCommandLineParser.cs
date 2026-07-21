using AgentUp.Installers.Features.Installation.Models;

namespace AgentUp.InstallerApp.Features.Installation.TerminalIntegration;

internal static class InstallerCommandLineParser
{
    internal static bool TryComponentAction(
        string[] args,
        string argument,
        IReadOnlyList<ProductComponent> components,
        out ProductComponent component)
    {
        component = default!;
        for (var index = 0; index < args.Length; index++)
        {
            if (!args[index].Equals(argument, StringComparison.OrdinalIgnoreCase))
                continue;
            if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
                throw new InvalidOperationException($"{argument} requires a component target.");

            component = ParseComponent(args[index + 1], components);
            return true;
        }

        return false;
    }

    private static ProductComponent ParseComponent(string value, IReadOnlyList<ProductComponent> components)
    {
        var found = components.FirstOrDefault(c => c.Id.Equals(value, StringComparison.OrdinalIgnoreCase));
        if (found is null)
        {
            var expected = string.Join(", ", components.Select(c => c.Id));
            throw new InvalidOperationException($"Unknown installer component '{value}'. Expected {expected}.");
        }
        return found;
    }
}
