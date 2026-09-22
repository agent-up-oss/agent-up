using AgentUp.Capabilities.Codex.Composition;

if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.Error.WriteLine("usage: AgentUp.Capabilities.Codex <package-directory>");
    return 1;
}

CodexPackerHost.Pack(args[0]);
return 0;
