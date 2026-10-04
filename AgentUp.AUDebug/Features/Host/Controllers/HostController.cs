using AgentUp.AUDebug.Features.Desktop.Controllers;
using AgentUp.AUDebug.Features.Docs.Controllers;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Host.Interfaces;
using AgentUp.AUDebug.Features.Host.Services;
using AgentUp.AUDebug.Features.Mobile.Controllers;
using AgentUp.AUDebug.Features.Screens.Controllers;
using AgentUp.AUDebug.Features.Screenshots.Controllers;
using AgentUp.AUDebug.Features.Test.Controllers;

namespace AgentUp.AUDebug.Features.Host.Controllers;

public sealed class HostController
{
    private readonly HostCommandService _host;
    private readonly DesktopController _desktop;
    private readonly MobileController _mobile;
    private readonly DocsController _docs;
    private readonly TestController _tests;
    private readonly ScreenshotsController _screenshots;
    private readonly ScreensController _screens;
    private readonly IDebugArgParser _parser;
    private readonly DebugOutputService _output;

    public HostController(
        HostCommandService host,
        DesktopController desktop,
        MobileController mobile,
        DocsController docs,
        TestController tests,
        ScreenshotsController screenshots,
        ScreensController screens,
        IDebugArgParser parser,
        DebugOutputService output)
    {
        _host = host;
        _desktop = desktop;
        _mobile = mobile;
        _docs = docs;
        _tests = tests;
        _screenshots = screenshots;
        _screens = screens;
        _parser = parser;
        _output = output;
    }

    public Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
        => ExecuteAsync(args, cancellationToken);

    private async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken)
    {
        var (command, error) = _parser.Parse(args);
        if (error is not null)
            return _output.WriteError(error);

        return await Route(_host, _desktop, _mobile, _docs, _tests, _screenshots, _screens, _output, command!, cancellationToken);
    }

    private static Task<int> Route(
        HostCommandService host,
        DesktopController desktop,
        MobileController mobile,
        DocsController docs,
        TestController tests,
        ScreenshotsController screenshots,
        ScreensController screens,
        DebugOutputService output,
        DebugCommandDto command,
        CancellationToken cancellationToken)
        => command.Verb switch
        {
            "help" => Task.FromResult(output.WriteHelp()),
            "up" => WriteAsync(output, () => host.UpAsync(command, cancellationToken)),
            "down" => WriteAsync(output, () => host.DownAsync(command, cancellationToken)),
            "status" => WriteAsync(output, () => host.StatusAsync(command, cancellationToken)),
            "desktop" => WriteAsync(output, () => desktop.RunAsync(command, cancellationToken)),
            "mobile" => WriteAsync(output, () => mobile.RunAsync(command, cancellationToken)),
            "docs" => WriteAsync(output, () => docs.ScreenshotAsync(command, cancellationToken)),
            "screenshots" => WriteAsync(output, () => screenshots.RunAsync(command, cancellationToken)),
            "screens" => WriteAsync(output, () => screens.RunAsync(command, cancellationToken)),
            "test" or "build" => WriteAsync(output, () => tests.RunAsync(command, cancellationToken)),
            _ => Task.FromResult(output.WriteError($"Error: unknown command '{command.Verb}'."))
        };

    private static async Task<int> WriteAsync(DebugOutputService output, Func<Task<CommandResultDto>> action)
        => output.Write(await action());
}
