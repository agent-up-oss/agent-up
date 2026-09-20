using AgentUp.Capabilities.Claude.Composition;

if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.Error.WriteLine("usage: AgentUp.Capabilities.Claude <package-directory>");
    return 1;
}

ClaudePackerHost.Pack(args[0]);
return 0;
