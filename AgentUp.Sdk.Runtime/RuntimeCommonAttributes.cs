namespace AgentUp.Sdk.Runtime;

public static class RuntimeCommonAttributes
{
    public const string Name = "name";
    public const string Path = "path";
    public const string Environment = "environment";
    public const string EnvironmentFiles = "environmentFiles";
    public const string Ports = "ports";
    public const string TechnologyVersion = "sdk";
    public const string Database = "database";

    public static readonly IReadOnlyList<string> Names =
    [
        Name,
        Path,
        Environment,
        EnvironmentFiles,
        Ports,
        TechnologyVersion,
        Database
    ];
}
