using AgentUp.PackageSmoke.Features.SmokeRuns.Factories;

var controller = SmokeCommandControllerFactory.Create();
return await controller.ExecuteAsync(args, Console.Out, Console.Error);
