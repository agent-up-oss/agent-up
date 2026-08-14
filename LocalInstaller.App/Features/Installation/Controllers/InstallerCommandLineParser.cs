using LocalInstaller.Core.Features.Installation.Models;

namespace LocalInstaller.App.Features.Installation.Controllers;

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
        if (found is not null)
            return found;

        if (Enum.TryParse<InstallerComponentTarget>(value, ignoreCase: true, out var target))
        {
            var matches = components.Where(c => ComponentTarget(c) == target).ToArray();
            if (matches.Length == 1)
                return matches[0];
            if (matches.Length > 1)
            {
                var ids = string.Join(", ", matches.Select(c => c.Id));
                throw new InvalidOperationException($"Installer component target '{value}' is ambiguous. Use one of: {ids}.");
            }
        }

        var expected = string.Join(", ", components.Select(c => c.Id));
        throw new InvalidOperationException($"Unknown installer component '{value}'. Expected {expected}.");
    }

    private static InstallerComponentTarget? ComponentTarget(ProductComponent component)
        => component.Target
           ?? (Enum.TryParse<InstallerComponentTarget>(component.Id, ignoreCase: true, out var target) ? target : null);
}
