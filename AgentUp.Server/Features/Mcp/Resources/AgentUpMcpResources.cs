using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentUp.Server.Features.Mcp.Controllers;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Features.Mcp.Resources;

[McpServerResourceType]
public sealed class AgentUpMcpResources
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly McpWorkspaceController _workspaces;
    private readonly McpContextController _context;

    public AgentUpMcpResources(McpWorkspaceController workspaces, McpContextController context)
    {
        _workspaces = workspaces;
        _context = context;
    }

    [McpServerResource(UriTemplate = "agent-up://context", Name = "Agent-Up Context", MimeType = "text/markdown")]
    [Description("Concise Agent-Up operating rules for AI agents.")]
    public string GetAgentUpContext() => _context.GetAgentUpContext();

    [McpServerResource(UriTemplate = "agent-up://agent-up-json", Name = "agent-up.json Format", MimeType = "text/markdown")]
    [Description("Current declarative agent-up.json format supported by Agent-Up.")]
    public string GetAgentUpJsonFormat() => _context.GetAgentUpJsonFormat();

    [McpServerResource(UriTemplate = "agent-up://workspaces", Name = "Agent-Up Workspaces", MimeType = "application/json")]
    [Description("All workspaces registered with Agent-Up Server.")]
    public string ListWorkspaces() => JsonSerializer.Serialize(_workspaces.List(), JsonOptions);

    [McpServerResource(UriTemplate = "agent-up://workspaces/{id}", Name = "Agent-Up Workspace", MimeType = "application/json")]
    [Description("A single workspace registered with Agent-Up Server.")]
    public string GetWorkspace(string id)
    {
        var workspace = _workspaces.GetById(id);
        return workspace is null
            ? JsonSerializer.Serialize(new { error = "Workspace not found." }, JsonOptions)
            : JsonSerializer.Serialize(workspace, JsonOptions);
    }
}
