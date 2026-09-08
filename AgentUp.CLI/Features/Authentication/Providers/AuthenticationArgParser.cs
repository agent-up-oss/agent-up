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

        return ReadPasswordFromConsole();
    }

    private static string ReadPasswordFromConsole()
    {
        Console.Write("Admin password: ");
        var buffer = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Count > 0)
                    buffer.RemoveAt(buffer.Count - 1);
                continue;
            }

            if (!char.IsControl(key.KeyChar))
                buffer.Add(key.KeyChar);
        }

        return new string(buffer.ToArray());
    }
}
