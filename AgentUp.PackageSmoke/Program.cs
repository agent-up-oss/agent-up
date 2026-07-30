#if EXCLUDE_AGENTUP_PRODUCT_CONFIGURATION
return 0;
#else
using AgentUp.PackageSmoke.Shared.Factories;

var controller = PackageSmokeServiceRegistry.CreateSmokeCommandController();
return await controller.ExecuteAsync(args, Console.Out, Console.Error);
#endif
