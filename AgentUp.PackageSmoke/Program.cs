using AgentUp.PackageSmoke.Shared.Factories;

var controller = PackageSmokeServiceRegistry.CreateSmokeCommandController();
return await controller.ExecuteAsync(args, Console.Out, Console.Error);
