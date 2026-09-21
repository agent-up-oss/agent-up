using AgentUp.Registry.Composition;

namespace AgentUp.Registry;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var registryRoot = builder.Configuration["AGENTUP_CAPABILITY_REGISTRY_PATH"]
            ?? Path.Join(builder.Environment.ContentRootPath, "capability-registry");
        RegistryServiceRegistration.Configure(builder, registryRoot);
        var app = builder.Build();
        app.MapControllers();
        app.Run();
    }
}
