using AgentUp.Registry.Composition;

var builder = WebApplication.CreateBuilder(args);
var registryRoot = builder.Configuration["AGENTUP_CAPABILITY_REGISTRY_PATH"]
    ?? Path.Join(builder.Environment.ContentRootPath, "capability-registry");
RegistryServiceRegistration.Configure(builder, registryRoot);
var app = builder.Build();
app.MapControllers();
app.Run();

public partial class Program;
