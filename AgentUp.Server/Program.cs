using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using AgentUp.Server.Composition;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSystemd();
builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "Agent-Up Server";
});

ServiceRegistration.Configure(builder, ResolveDataDirectory());

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapMcp("/mcp");

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
        return Path.Join(Path.GetTempPath(), "AgentUp", checkoutId);
    }

    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    if (!string.IsNullOrWhiteSpace(localAppData))
        return Path.Join(localAppData, "AgentUp");

    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        return Path.Join("/Library", "Application Support", "Agent-Up");

    return Path.Join("/var", "lib", "agent-up");
}

namespace AgentUp.Server
{
    public partial class Program;
}
