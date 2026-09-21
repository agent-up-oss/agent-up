using AgentUp.Registry.Composition;

namespace AgentUp.Registry;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        RegistryServiceRegistration.Configure(builder);
        var app = builder.Build();
        app.MapControllers();
        app.Run();
    }
}
