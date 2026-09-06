using AgentUp.CommitPolicy.Features.CommitPolicy.Models;

namespace AgentUp.CommitPolicy.Features.CommitPolicy.Providers;

public sealed class CommitBuildPlanProvider
{
    public IReadOnlyList<string> ResolveCommands(CommitsConfiguration config, IReadOnlyList<string> files)
    {
        var touchedProjects = files
            .Select(RootDirectory)
            .Where(root => config.Projects.ContainsKey(root))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var required = new HashSet<string>(StringComparer.Ordinal);
        foreach (var project in touchedProjects)
            CollectDependents(project, config.Projects, required);

        var commands = new List<string>();
        AddRange(commands, config.Build);
        AddRange(commands, config.Test);
        foreach (var projectName in required.OrderBy(name => name, StringComparer.Ordinal))
            AddRange(commands, config.Projects[projectName].Test);

        return commands;
    }

    private static void CollectDependents(
        string project,
        IReadOnlyDictionary<string, CommitsProjectConfiguration> projects,
        HashSet<string> required)
    {
        if (!required.Add(project))
            return;

        foreach (var (name, definition) in projects)
        {
            if (definition.DependsOn.Contains(project, StringComparer.Ordinal))
                CollectDependents(name, projects, required);
        }
    }

    private static void AddRange(List<string> commands, IReadOnlyList<string> values)
    {
        foreach (var value in values)
        {
            if (!commands.Contains(value, StringComparer.Ordinal))
                commands.Add(value);
        }
    }

    private static string RootDirectory(string path)
        => path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
}
