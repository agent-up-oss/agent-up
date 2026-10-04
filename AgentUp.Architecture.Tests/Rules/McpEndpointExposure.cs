using AgentUp.Architecture.Tests.Fixtures;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AgentUp.Architecture.Tests.Rules;

/// <summary>
/// The per-endpoint MCP tool allowlists in McpEndpointSessionProvider are hand-written
/// duplicates of the [McpServerTool] declarations. They were correct by discipline alone;
/// these rules make that structural, so a new tool cannot be unreachable and a removed
/// tool cannot leave a stale entry behind.
/// </summary>
[TestFixture]
public sealed class McpEndpointExposure
{
    private const string SessionProviderPath = "AgentUp.Server/Shared/Providers/McpEndpointSessionProvider.cs";

    [Test]
    public void Every_declared_mcp_tool_is_exposed_by_exactly_one_endpoint()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var declared = DeclaredToolNames(root);
        var allowlisted = AllowlistedToolNames(root);

        var unreachable = declared
            .Where(name => !allowlisted.Contains(name))
            .Order(StringComparer.Ordinal)
            .Select(name => $"{name} is declared but no MCP endpoint exposes it")
            .ToArray();

        Assert.That(unreachable, Is.Empty,
            "A tool missing from every endpoint allowlist in McpEndpointSessionProvider is registered but unreachable.");
    }

    [Test]
    public void Every_allowlisted_mcp_tool_name_resolves_to_a_declared_tool()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var declared = DeclaredToolNames(root);
        var allowlisted = AllowlistedToolNames(root);

        var stale = allowlisted
            .Where(name => !declared.Contains(name))
            .Order(StringComparer.Ordinal)
            .Select(name => $"{name} is allowlisted but no [McpServerTool] declares it")
            .ToArray();

        Assert.That(stale, Is.Empty,
            "Remove stale endpoint allowlist entries when a tool is renamed or deleted.");
    }

    private static HashSet<string> DeclaredToolNames(string root)
        => ArchitectureFixture.ProductionSourceFiles(root)
            .SelectMany(path => ToolNamesIn(path))
            .ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<string> ToolNamesIn(string path)
    {
        var (_, rootNode) = ArchitectureFixture.ParseSourceFile(path);
        return rootNode.DescendantNodes()
            .OfType<AttributeSyntax>()
            .Where(attribute => attribute.Name.ToString() is "McpServerTool" or "McpServerToolAttribute")
            .SelectMany(attribute => attribute.ArgumentList?.Arguments ?? default)
            .Where(argument => argument.NameEquals?.Name.Identifier.Text == "Name")
            .Select(argument => argument.Expression)
            .OfType<LiteralExpressionSyntax>()
            .Select(literal => literal.Token.ValueText);
    }

    /// <summary>
    /// Reads the string literals inside the endpoint HashSet initializers. Those sets are
    /// the only thing that decides which tools an endpoint serves.
    /// </summary>
    private static HashSet<string> AllowlistedToolNames(string root)
    {
        var (_, rootNode) = ArchitectureFixture.ParseSourceFile(Path.Join(root, SessionProviderPath));

        return rootNode.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .Where(field => field.Declaration.Type.ToString().Contains("HashSet<string>", StringComparison.Ordinal))
            .SelectMany(field => field.DescendantNodes().OfType<LiteralExpressionSyntax>())
            .Select(literal => literal.Token.ValueText)
            .Where(value => value.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
    }

    [Test]
    public void Mcp_catalog_names_only_declared_tools()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var declared = DeclaredToolNames(root);
        var markdown = SliceToolCatalog(root);
        var named = System.Text.RegularExpressions.Regex.Matches(markdown, "`([a-z][a-z0-9]*(?:_[a-z0-9]+)+)`")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .Where(name => !declared.Contains(name))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.That(named, Is.Empty,
            "Developer Guide slice pages named MCP tools that no [McpServerTool] declares.");
    }

    [Test]
    public void Mcp_catalog_documents_verification_server()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var markdown = SliceToolCatalog(root);
        Assert.That(markdown, Does.Contain("/mcp/verification"));
    }

    [Test]
    public void Every_mcp_workspace_target_parameter_is_covered_by_the_binding_guard()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var guarded = GuardedArgumentNames(root);
        var sessionProvider = File.ReadAllText(Path.Join(root, SessionProviderPath));
        var uncovered = WorkspaceTargetParameters(root)
            .Where(parameter => !guarded.Contains(parameter.Name))
            .Select(parameter => $"{parameter.Tool} parameter '{parameter.Name}' is a workspace target but is not in McpWorkspaceTargetArguments")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.That(sessionProvider, Does.Contain("McpWorkspaceBindingController"));
        Assert.That(uncovered, Is.Empty,
            "MCP tools that take a workspace id or worktree path must use a name the workspace-binding guard already recognizes.");
    }

    private static HashSet<string> GuardedArgumentNames(string root)
    {
        var (_, rootNode) = ArchitectureFixture.ParseSourceFile(
            Path.Join(root, "AgentUp.Server/Features/Authentication/Models/McpWorkspaceTargetArguments.cs"));
        return rootNode.DescendantNodes()
            .OfType<LiteralExpressionSyntax>()
            .Select(literal => literal.Token.ValueText)
            .Where(value => value.Length > 0 && value.All(character => char.IsLetter(character)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<(string Tool, string Name)> WorkspaceTargetParameters(string root)
    {
        foreach (var path in ArchitectureFixture.ProductionSourceFiles(root))
        {
            var (_, rootNode) = ArchitectureFixture.ParseSourceFile(path);
            foreach (var method in rootNode.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                var toolName = ToolName(method);
                if (toolName is null)
                    continue;

                foreach (var parameter in method.ParameterList.Parameters)
                {
                    var name = parameter.Identifier.Text;
                    var description = ParameterDescription(parameter);
                    if (IsWorkspaceTarget(name, description))
                        yield return (toolName, name);
                }
            }
        }
    }

    private static string? ToolName(MethodDeclarationSyntax method)
    {
        return method.AttributeLists
            .SelectMany(list => list.Attributes)
            .Where(attribute => attribute.Name.ToString() is "McpServerTool" or "McpServerToolAttribute")
            .SelectMany(attribute => attribute.ArgumentList?.Arguments ?? default)
            .Where(argument => argument.NameEquals?.Name.Identifier.Text == "Name")
            .Select(argument => argument.Expression)
            .OfType<LiteralExpressionSyntax>()
            .Select(literal => literal.Token.ValueText)
            .FirstOrDefault();
    }

    private static string? ParameterDescription(ParameterSyntax parameter)
    {
        return parameter.AttributeLists
            .SelectMany(list => list.Attributes)
            .Where(attribute => attribute.Name.ToString() is "Description" or "DescriptionAttribute")
            .SelectMany(attribute => attribute.ArgumentList?.Arguments ?? default)
            .Select(argument => argument.Expression)
            .OfType<LiteralExpressionSyntax>()
            .Select(literal => literal.Token.ValueText)
            .FirstOrDefault();
    }

    private static bool IsWorkspaceTarget(string name, string? description)
        => name.Equals("workspaceId", StringComparison.OrdinalIgnoreCase)
           || name.Equals("worktreePath", StringComparison.OrdinalIgnoreCase)
           || name.Equals("repositoryPath", StringComparison.OrdinalIgnoreCase)
           || (name.Equals("id", StringComparison.OrdinalIgnoreCase)
               && description is not null
               && description.Contains("workspace", StringComparison.OrdinalIgnoreCase));

    private static string SliceToolCatalog(string root)
    {
        var slices = new[]
        {
            "workspaces", "applications", "git", "commits", "agents",
            "browser", "diagnostics", "verification", "configuration",
        };
        var files = slices
            .Select(slice => Path.Join(root, "docs/developer-guide", slice))
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.md", SearchOption.AllDirectories))
            .Where(file => !file.EndsWith("-assessment.md", StringComparison.Ordinal))
            .Append(Path.Join(root, "docs/developer-guide/index.md"));
        return string.Join('\n', files.Select(File.ReadAllText));
    }
}
