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
    public static void Configure(WebApplicationBuilder builder, string registryRoot)
    {
        builder.Services.AddSingleton<IRegistryPathValidator>(_ => new RegistryPathValidator(registryRoot));
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
