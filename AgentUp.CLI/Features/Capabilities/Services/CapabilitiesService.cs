using AgentUp.CLI.Features.Capabilities.DTOs;
using AgentUp.CLI.Features.Capabilities.Interfaces;
using AgentUp.CLI.Features.Capabilities.Models;
using AgentUp.CLI.Shared.Providers;

namespace AgentUp.CLI.Features.Capabilities.Services;

public sealed class CapabilitiesService(
    ICapabilityModulesApi api,
    ICapabilityPacker packer,
    ICapabilitiesArgParser parser)
{
    public Task<CapabilitiesCommandResult> RunAsync(string[] args, string workingDirectory, CancellationToken cancellationToken = default)
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
            "enable" => EnableAsync(command.Id!, command.Version, cancellationToken),
            "disable" => DisableAsync(command.Id!, cancellationToken),
            "bootstrap" => BootstrapAsync(workingDirectory, cancellationToken),
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

    private async Task<CapabilitiesCommandResult> EnableAsync(string id, string? version, CancellationToken cancellationToken)
    {
        try
        {
            var listed = await api.ListAsync(cancellationToken);
            var match = Find(listed, id, version);
            if (match is null)
                return CapabilitiesCommandResult.Fail(UnknownCapability(id, version));
            if (match.Enabled)
                return CapabilitiesCommandResult.Ok($"Capability '{match.Id}' is already enabled.");

            var enabled = await api.EnableAsync(id, version, cancellationToken);
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
            var listed = await api.ListAsync(cancellationToken);
            var match = Find(listed, id);
            if (match is null)
                return CapabilitiesCommandResult.Fail($"Error: Capability '{id}' is not in the local registry.");

            var disabled = await api.DisableAsync(id, cancellationToken);
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

    private async Task<CapabilitiesCommandResult> BootstrapAsync(string workingDirectory, CancellationToken cancellationToken)
    {
        var packed = await packer.PackFirstPartyAsync(workingDirectory, cancellationToken);
        if (!packed.Succeeded)
            return CapabilitiesCommandResult.Fail(packed.Message);

        var lines = new List<string> { packed.Message };
        try
        {
            foreach (var id in FirstPartyCapabilities.Ids)
            {
                var listed = await api.ListAsync(cancellationToken);
                var match = Find(listed, id);
                if (match is null)
                    return CapabilitiesCommandResult.Fail(
                        string.Join(Environment.NewLine, lines.Append($"Error: Capability '{id}' is not in the local registry.")));

                if (match.Enabled)
                {
                    lines.Add($"Capability '{match.Id}' is already enabled.");
                    continue;
                }

                var enabled = await api.EnableAsync(id, null, cancellationToken);
                lines.Add($"Enabled {enabled.Id} {enabled.Version}.");
            }

            return CapabilitiesCommandResult.Ok(string.Join(Environment.NewLine, lines));
        }
        catch (AuthenticationRequiredException ex)
        {
            return CapabilitiesCommandResult.Fail(ex.Message);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return CapabilitiesCommandResult.Fail($"Error: Failed to enable packed capabilities: {ex.Message}");
        }
    }

    private static CapabilityModuleDto? Find(
        IReadOnlyList<CapabilityModuleDto> modules,
        string id,
        string? version = null)
        => modules.FirstOrDefault(module =>
            module.Id.Equals(id, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(version)
                || module.Version.Equals(version, StringComparison.Ordinal)));

    private static string UnknownCapability(string id, string? version)
        => string.IsNullOrWhiteSpace(version)
            ? $"Error: Capability '{id}' is not in the local registry."
            : $"Error: Capability '{id}' version '{version}' is not in the local registry.";
}
