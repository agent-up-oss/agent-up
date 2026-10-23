using AgentUp.Capabilities.Forgejo.Composition;

if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.Error.WriteLine("usage: AgentUp.Capabilities.Forgejo <package-directory>");
    return 1;
}

ForgejoPackerHost.Pack(args[0]);
return 0;
