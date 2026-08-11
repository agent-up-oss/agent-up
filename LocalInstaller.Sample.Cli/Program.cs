using LocalInstaller.Sample;

if (args.Contains("--version", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine(typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0");
    return 0;
}

if (args.Contains("--health", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine($"{SampleProduct.Name} CLI is healthy.");
    return 0;
}

Console.WriteLine($"{SampleProduct.Name} CLI");
Console.WriteLine($"Configuration file: {SampleProduct.WorkspaceConfigFileName}");
return 0;
