using AgentUp.Capabilities.Dotnet.Composition;

if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.Error.WriteLine("usage: AgentUp.Capabilities.Dotnet <package-directory>");
    return 1;
}

DotnetPackerHost.Pack(args[0]);
return 0;
