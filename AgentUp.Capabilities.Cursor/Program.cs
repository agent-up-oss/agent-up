using AgentUp.Capabilities.Cursor.Composition;

if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.Error.WriteLine("usage: AgentUp.Capabilities.Cursor <package-directory>");
    return 1;
}

CursorPackerHost.Pack(args[0]);
return 0;
