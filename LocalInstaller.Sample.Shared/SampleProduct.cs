namespace LocalInstaller.Sample;

public static class SampleProduct
{
    public const string Name = "LocalInstaller Sample";
    public const string Slug = "localinstaller-sample";
    public const string EnvironmentPrefix = "LOCALINSTALLERSAMPLE";
    public const string UpgradeCode = "8A7D9F93-7D7E-4F02-AE5E-6FCFDE2FC6A1";
    public const string ServerUrl = "http://127.0.0.1:50241";
    public const string WorkspaceConfigFileName = "localinstaller-sample.json";
    public const string FakeInstallerVariable = EnvironmentPrefix + "_INSTALLER_FAKE";
    public const string NixOsLookupOnlyVariable = EnvironmentPrefix + "_INSTALLER_NIXOS_LOOKUP_ONLY";
}
