namespace AgentUp.Sdk.Common;

public sealed record NixPackageDeclaration(
    string Package,
    string? NixpkgsRev = null,
    string? NixpkgsSha256 = null);
