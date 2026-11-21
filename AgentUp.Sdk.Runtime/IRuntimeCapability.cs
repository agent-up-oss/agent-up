using System.Text.Json;
using AgentUp.Sdk.Common;

namespace AgentUp.Sdk.Runtime;

public interface IRuntimeCapability : ICapability
{
    string SectionName { get; }

    IReadOnlyList<NixPackageDeclaration> NixPackages { get; }

    /// <summary>Commands this capability's Nix packages put on the path, so a managed launch of one is wrapped in its shell.</summary>
    IReadOnlyList<string> Provides { get; }

    IReadOnlyList<RuntimeAttributeSpec> ExtraAttributes { get; }

    RuntimeBindResult Bind(IReadOnlyList<IReadOnlyDictionary<string, JsonElement>> items);

    RuntimeDeliverResult Deliver(string? technologyVersion);

    RuntimeHostResult Host(RuntimeHostRequest app);
}
