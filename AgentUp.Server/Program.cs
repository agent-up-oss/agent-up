using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IWorkspaceRegistry, WorkspaceRegistry>();

var app = builder.Build();

app.MapWorkspaces();

app.Run();

namespace AgentUp.Server
{
    public partial class Program;
}
