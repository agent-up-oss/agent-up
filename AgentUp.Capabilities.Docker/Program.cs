using AgentUp.Capabilities.Docker.Composition;

if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.Error.WriteLine("usage: AgentUp.Capabilities.Docker <package-directory>");
    return 1;
}

DockerPackerHost.Pack(args[0]);
return 0;
