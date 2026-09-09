namespace AgentUp.CLI.Features.Authentication.Controllers;

public sealed class AuthenticationController(
    AuthLoginCommand login,
    AuthLogoutCommand logout,
    AuthStatusCommand status,
    TextWriter output)
{
    public Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
        => ResolveCommand(args, login, logout, status, output)(cancellationToken);

    private static Func<CancellationToken, Task<int>> ResolveCommand(
        string[] args,
        AuthLoginCommand login,
        AuthLogoutCommand logout,
        AuthStatusCommand status,
        TextWriter output)
    {
        var subcommand = args.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal)) ?? "";
        var remaining = args.SkipWhile(argument => argument != subcommand).Skip(1).ToArray();
        return subcommand switch
        {
            "login" => ct => login.RunAsync(remaining, ct),
            "logout" => logout.RunAsync,
            "status" => status.RunAsync,
            _ => _ =>
            {
                output.WriteLine("Usage: agent-up auth <login|logout|status> [--server <url>]");
                output.WriteLine("  login   Authenticate with the server admin password");
                output.WriteLine("  logout  Remove the stored access token for this server");
                output.WriteLine("  status  Show whether authentication is required and configured");
                return Task.FromResult(0);
            }
        };
    }
}
