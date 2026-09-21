using AgentUp.Registry.Features.LocalStore.Controllers;
using AgentUp.Registry.Features.LocalStore.Interfaces;
using AgentUp.Registry.Features.LocalStore.Providers;
using AgentUp.Registry.Features.LocalStore.Services;
using AgentUp.Registry.Features.Packages.Controllers;
using AgentUp.Registry.Features.Packages.Services;
using AgentUp.Registry.Features.RemoteCatalog.Controllers;
using AgentUp.Registry.Features.RemoteCatalog.Providers;
using AgentUp.Registry.Features.RemoteCatalog.Services;
using AgentUp.Registry.Shared.Interfaces;
using AgentUp.Registry.Shared.Providers;

namespace AgentUp.Registry.Composition;

public static class RegistryServiceRegistration
{
    /// <summary>
    /// Registers the registry against a root the caller already resolved, which is how the Server
    /// hosts it.
    /// </summary>
    public static void Configure(WebApplicationBuilder builder, string registryRoot)
        => Configure(builder, _ => registryRoot);

    /// <summary>
    /// Registers the registry against the root its own configuration names, falling back to a
    /// <c>capability-registry</c> directory beside the content root.
    /// </summary>
    /// <remarks>
    /// The root is resolved when the validator is first requested, not while the builder is being
    /// assembled. A test host adds its configuration during <c>Build()</c>, so a root read any
    /// earlier is the content-root fallback no matter what the test asked for - which is how test
    /// runs used to write packages into the checked-out <c>AgentUp.Registry</c> directory.
    /// </remarks>
    public static void Configure(WebApplicationBuilder builder)
        => Configure(builder, ResolveRegistryRoot);

    private static string ResolveRegistryRoot(IServiceProvider services)
        => services.GetRequiredService<IConfiguration>()["AGENTUP_CAPABILITY_REGISTRY_PATH"]
           ?? Path.Join(services.GetRequiredService<IHostEnvironment>().ContentRootPath, "capability-registry");

    private static void Configure(WebApplicationBuilder builder, Func<IServiceProvider, string> registryRoot)
    {
        builder.Services.AddSingleton<IRegistryPathValidator>(services =>
            new RegistryPathValidator(registryRoot(services)));
        builder.Services.AddSingleton<CapabilityPackageValidator>();
        builder.Services.AddSingleton<CapabilityTemplateRenderer>();
        builder.Services.AddSingleton<CapabilityPackageController>();
        builder.Services.AddSingleton<ILocalRegistryDirectoryStore, LocalRegistryDirectoryStore>();
        builder.Services.AddSingleton<LocalRegistryService>();
        builder.Services.AddSingleton<LocalRegistryController>();
        builder.Services.AddSingleton<PackageArchiveProvider>();
        builder.Services.AddSingleton<RemoteCatalogService>();
        builder.Services.AddSingleton<RemoteCatalogController>();
        builder.Services.AddControllers();
    }
}
