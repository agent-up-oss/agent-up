using AgentUp.CLI.Features.Capabilities.DTOs;
using AgentUp.CLI.Features.Capabilities.Interfaces;
using AgentUp.CLI.Tests.Support;

namespace AgentUp.CLI.Tests.Fake;

internal sealed class FakeCapabilityModulesApi : ICapabilityModulesApi
{
    public List<CapabilityModuleDto> Modules { get; set; } = [CliDomain.Capability().Build()];

    public string? EnabledId { get; private set; }

    public string? EnabledVersion { get; private set; }

    public string? DisabledId { get; private set; }

    public Exception? ListError { get; set; }

    public Exception? EnableError { get; set; }

    public Exception? DisableError { get; set; }

    public Task<IReadOnlyList<CapabilityModuleDto>> ListAsync(CancellationToken cancellationToken = default)
        => ListError is null
            ? Task.FromResult<IReadOnlyList<CapabilityModuleDto>>(Modules)
            : Task.FromException<IReadOnlyList<CapabilityModuleDto>>(ListError);

    public Task<CapabilityModuleDto> EnableAsync(string id, string? version, CancellationToken cancellationToken = default)
    {
        if (EnableError is not null)
            return Task.FromException<CapabilityModuleDto>(EnableError);

        EnabledId = id;
        EnabledVersion = version;
        var enabled = CliDomain.Capability().Named(id).Enabled().Build();
        Replace(enabled);
        return Task.FromResult(enabled);
    }

    public Task<CapabilityModuleDto> DisableAsync(string id, CancellationToken cancellationToken = default)
    {
        if (DisableError is not null)
            return Task.FromException<CapabilityModuleDto>(DisableError);

        DisabledId = id;
        var disabled = CliDomain.Capability().Named(id).Build();
        Replace(disabled);
        return Task.FromResult(disabled);
    }

    private void Replace(CapabilityModuleDto module)
    {
        Modules = Modules
            .Where(existing => !existing.Id.Equals(module.Id, StringComparison.OrdinalIgnoreCase))
            .Append(module)
            .ToList();
    }
}
