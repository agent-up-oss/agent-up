using AgentUp.Capabilities.Github.Composition;

if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.Error.WriteLine("usage: AgentUp.Capabilities.Github <package-directory>");
    return 1;
}

GithubPackerHost.Pack(args[0]);
return 0;
