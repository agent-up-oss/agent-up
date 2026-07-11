using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.Repositories;
using AgentUp.Server.Features.Workspaces.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var dataDir = ResolveDataDirectory();
builder.Services.AddSingleton<IWorkspaceRepository>(_ =>
    new JsonWorkspaceRepository(Path.Combine(dataDir, "workspaces.json")));
builder.Services.AddSingleton<IOutputRepository>(_ =>
    new FileOutputRepository(dataDir));
builder.Services.AddSingleton<WorkspaceRegistry>();
builder.Services.AddSingleton<IWorkspaceRegistry>(sp => sp.GetRequiredService<WorkspaceRegistry>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<WorkspaceRegistry>());
builder.Services.AddSingleton<WorkspaceProcessManager>();
builder.Services.AddSingleton<IWorkspaceProcessManager>(sp => sp.GetRequiredService<WorkspaceProcessManager>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<WorkspaceProcessManager>());

var app = builder.Build();

app.MapWorkspaces();

app.Run();

string ResolveDataDirectory()
{
    var configured = builder.Configuration["Storage:DataDirectory"];
    if (!string.IsNullOrWhiteSpace(configured))
        return configured;

    if (builder.Environment.IsDevelopment())
    {
        var checkoutId = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(AppContext.BaseDirectory)))[..16];
        return Path.Combine(Path.GetTempPath(), "AgentUp", checkoutId);
    }

    return Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AgentUp");
}

namespace AgentUp.Server
{
    public partial class Program;
}
