using AgentUp.CLI.Features.Capabilities.DTOs;
using AgentUp.CLI.Features.Capabilities.Interfaces;
using AgentUp.CLI.Features.Capabilities.Models;
using AgentUp.CLI.Shared.Providers;

namespace AgentUp.CLI.Features.Capabilities.Services;

public sealed class CapabilitiesService(
    ICapabilityModulesApi api,
    ICapabilitiesArgParser parser)
{
    public Task<CapabilitiesCommandResult> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var (command, error) = parser.Parse(args);
        if (command is null)
        {
            var text = error ?? CapabilitiesHelp.Text;
            return Task.FromResult(CapabilitiesCommandResult.Fail(text, exitCode: text == CapabilitiesHelp.Text ? 0 : 1));
        }

        return command.Verb switch
        {
            "list" => ListAsync(cancellationToken),
            "enable" => EnableAsync(command.Id!, cancellationToken),
            "disable" => DisableAsync(command.Id!, cancellationToken),
            "bootstrap" => BootstrapAsync(cancellationToken),
            _ => Task.FromResult(CapabilitiesCommandResult.Fail(CapabilitiesHelp.Text, exitCode: 0))
        };
    }

    private async Task<CapabilitiesCommandResult> ListAsync(CancellationToken cancellationToken)
    {
        try
        {
            var modules = await api.ListAsync(cancellationToken);
            return CapabilitiesCommandResult.Ok(CapabilitiesOutputService.FormatList(modules));
        }
        catch (AuthenticationRequiredException ex)
        {
            return CapabilitiesCommandResult.Fail(ex.Message);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return CapabilitiesCommandResult.Fail($"Error: Failed to list capabilities: {ex.Message}");
        }
    }

    private async Task<CapabilitiesCommandResult> EnableAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            var match = Find(await api.ListAsync(cancellationToken), id);
            if (match is null)
                return CapabilitiesCommandResult.Fail(UnknownCapability(id));
            if (match.Enabled)
                return CapabilitiesCommandResult.Ok($"Capability '{match.Id}' is already enabled.");
            if (!match.Available)
                return CapabilitiesCommandResult.Fail(NotFound(match));

            var enabled = await api.EnableAsync(match.Id, cancellationToken);
            return CapabilitiesCommandResult.Ok($"Enabled {enabled.Id} {enabled.Version}.");
        }
        catch (AuthenticationRequiredException ex)
        {
            return CapabilitiesCommandResult.Fail(ex.Message);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return CapabilitiesCommandResult.Fail($"Error: Failed to enable '{id}': {ex.Message}");
        }
    }

    private async Task<CapabilitiesCommandResult> DisableAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            var match = Find(await api.ListAsync(cancellationToken), id);
            if (match is null)
                return CapabilitiesCommandResult.Fail(UnknownCapability(id));

            var disabled = await api.DisableAsync(match.Id, cancellationToken);
            return CapabilitiesCommandResult.Ok($"Disabled {disabled.Id}.");
        }
        catch (AuthenticationRequiredException ex)
        {
            return CapabilitiesCommandResult.Fail(ex.Message);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return CapabilitiesCommandResult.Fail($"Error: Failed to disable '{id}': {ex.Message}");
        }
    }

    /// <summary>
    /// Enables every built-in capability whose software the Server finds. One the Server cannot
    /// run is listed with what is missing rather than failing the command, so a host without, say,
    /// a Cursor CLI still gets everything else.
    /// </summary>
    private async Task<CapabilitiesCommandResult> BootstrapAsync(CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        try
        {
            foreach (var module in await api.ListAsync(cancellationToken))
            {
                if (module.Enabled)
                {
                    lines.Add($"Capability '{module.Id}' is already enabled.");
                    continue;
                }

                if (!module.Available)
                {
                    lines.Add($"Skipped {module.Id}: {string.Join(", ", module.Missing)} not found.");
                    continue;
                }

                var enabled = await api.EnableAsync(module.Id, cancellationToken);
                lines.Add($"Enabled {enabled.Id} {enabled.Version}.");
            }

            return CapabilitiesCommandResult.Ok(lines.Count == 0 ? "The Server reports no capabilities." : string.Join(Environment.NewLine, lines));
        }
        catch (AuthenticationRequiredException ex)
        {
            return CapabilitiesCommandResult.Fail(ex.Message);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return CapabilitiesCommandResult.Fail(
                string.Join(Environment.NewLine, lines.Append($"Error: Failed to enable capabilities: {ex.Message}")));
        }
    }

    private static CapabilityModuleDto? Find(IReadOnlyList<CapabilityModuleDto> modules, string id)
        => modules.FirstOrDefault(module => module.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    private static string UnknownCapability(string id)
        => $"Error: Capability '{id}' is not built into this Server.";

    private static string NotFound(CapabilityModuleDto module)
        => string.Join(
            Environment.NewLine,
            new[] { $"Error: Capability '{module.Id}' cannot be enabled: {string.Join(", ", module.Missing)} not found." }
                .Concat(module.Messages.Select(message => "  " + message)));
}
