using System.Text.Json;
using AgentUp.Sdk.Common;

namespace AgentUp.Sdk.Runtime;

public interface IRuntimeCapability
{
    CapabilityIdentity Identity { get; }

    string SectionName { get; }

    IReadOnlyList<NixPackageDeclaration> NixPackages { get; }

    IReadOnlyList<RuntimeAttributeSpec> ExtraAttributes { get; }

    RuntimeBindResult Bind(IReadOnlyList<IReadOnlyDictionary<string, JsonElement>> items);

    RuntimeDeliverResult Deliver(string? technologyVersion);

    RuntimeHostResult Host(RuntimeHostRequest app);
}
