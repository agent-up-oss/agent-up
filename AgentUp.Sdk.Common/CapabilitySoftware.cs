namespace AgentUp.Sdk.Common;

/// <summary>
/// One executable a capability needs on the Server host.
/// </summary>
/// <param name="Executable">The command name, as it is looked up on the search path.</param>
/// <param name="DeliveredByNix">
/// True when the capability delivers the executable itself through its pinned Nix packages, so the
/// requirement is Nix rather than the executable. False when the executable has to be found on the
/// Server's search path already.
/// </param>
public sealed record CapabilitySoftware(string Executable, bool DeliveredByNix);
