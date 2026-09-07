namespace AgentUp.CLI.Features.Authentication.Providers;

public sealed class AuthenticationArgParser
{
    public string? ParsePassword(string[] args)
    {
        for (var index = 0; index < args.Length; index++)
        {
            if (args[index] == "--password" && index + 1 < args.Length)
                return args[index + 1];
        }

        if (Console.IsInputRedirected)
            return null;

        Console.Write("Admin password: ");
        return Console.ReadLine();
    }
}
