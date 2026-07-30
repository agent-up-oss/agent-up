#if EXCLUDE_AGENTUP_PRODUCT_CONFIGURATION
return 0;
#else
using AgentUp.Packaging.Shared.Factories;

return await new PackagingServiceRegistry().PackageCommands.ExecuteAsync(args);
#endif
