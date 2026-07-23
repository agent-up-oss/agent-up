using AgentUp.PackageSmoke.Shared.Providers;

namespace AgentUp.PackageSmoke.Features.InstalledServiceValidation.Providers;

public sealed class CapabilityWorkspaceProvider
{
    public string Prepare(string workDirectory)
    {
        var safeWorkDirectory = SafeSmokePaths.Root(workDirectory, nameof(workDirectory));
        var repo = SafeSmokePaths.Child(safeWorkDirectory, "capability-workspace");

        Directory.CreateDirectory(SafeSmokePaths.Child(repo, "SmokeDotnet"));
        File.WriteAllText(SafeSmokePaths.Child(repo, "SmokeDotnet", "SmokeDotnet.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(SafeSmokePaths.Child(repo, "SmokeDotnet", "Program.cs"), """
            var builder = WebApplication.CreateBuilder(args);
            var app = builder.Build();
            app.MapGet("/", () => "dotnet capability smoke");
            var port = Environment.GetEnvironmentVariable("WEB_PORT") ?? "5000";
            app.Run($"http://127.0.0.1:{port}");
            """);
        File.WriteAllText(SafeSmokePaths.Child(repo, "agent-up.json"), $$"""
            {
              "name": "Capability Lifecycle Smoke Workspace",
              "dotnet": [
                {
                  "name": "SmokeDotnet",
                  "sdk": "10.0.x",
                  "run": {
                    "project": "SmokeDotnet/SmokeDotnet.csproj"
                  },
                  "ports": [{ "variable": "WEB_PORT", "defaultPort": 5000, "protocol": "http" }]
                }
              ],
              "docker": [
                {
                  "name": "SmokeDocker",
                  "image": "{{DockerSmokeImage()}}",
                  "ports": [{ "variable": "DOCKER_WEB_PORT", "defaultPort": 80, "protocol": "http" }]
                }
              ]
            }
            """);

        return repo;
    }

    private static string DockerSmokeImage()
    {
        var image = Environment.GetEnvironmentVariable("AGENTUP_CAPABILITY_SMOKE_DOCKER_IMAGE");
        return string.IsNullOrWhiteSpace(image) ? "nginx:alpine" : image;
    }
}
