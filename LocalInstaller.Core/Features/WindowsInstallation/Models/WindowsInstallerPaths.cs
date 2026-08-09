namespace AgentUp.Installers.Features.WindowsInstallation.Models;

public sealed partial record WindowsInstallerPaths(
    string RootDirectory,
    string DesktopDirectory,
    string ServerDirectory,
    string CliDirectory,
    string TrayDirectory,
    string BinDirectory,
    string StartMenuShortcutPath,
    string CliShimName,
    string UninstallScriptName)
{
    public string DesktopExecutable => WindowsCombine(DesktopDirectory, "AgentUp.Desktop.exe");
    public string ServerExecutable => WindowsCombine(ServerDirectory, "AgentUp.Server.exe");
    public string TrayExecutable => WindowsCombine(TrayDirectory, "AgentUp.Tray.exe");
    public string CliExecutable => WindowsCombine(CliDirectory, "AgentUp.CLI.exe");
    public string CliShimPath => CliShimPathFor(CliShimName);
    public string CliShimPathFor(string cliShimName)
        => WindowsCombine(BinDirectory, WindowsInstallerManifest.RequireSafeCliShimFileName(cliShimName));
    public string UninstallScriptPath => WindowsCombine(RootDirectory, UninstallScriptName);

    public static WindowsInstallerPaths ForProduct(
        AgentUp.Installers.Features.Installation.Models.ProductManifest product,
        string programFilesRoot,
        string commonStartMenuRoot)
    {
        var root = WindowsCombine(programFilesRoot, product.ProductName);
        return new WindowsInstallerPaths(
            RootDirectory: root,
            DesktopDirectory: WindowsCombine(root, "desktop"),
            ServerDirectory: WindowsCombine(root, "server"),
            CliDirectory: WindowsCombine(root, "cli"),
            TrayDirectory: WindowsCombine(root, "tray"),
            BinDirectory: WindowsCombine(root, "bin"),
            StartMenuShortcutPath: WindowsCombine(commonStartMenuRoot, "Programs", product.ProductName, $"{product.ProductName}.lnk"),
            CliShimName: $"{product.Slug}.cmd",
            UninstallScriptName: $"uninstall-{product.Slug}.ps1");
    }

    public static WindowsInstallerPaths ForProduct(AgentUp.Installers.Features.Installation.Models.ProductManifest product)
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (string.IsNullOrWhiteSpace(programFiles))
            programFiles = @"C:\Program Files";

        var commonStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
        if (string.IsNullOrWhiteSpace(commonStartMenu))
            commonStartMenu = @"C:\ProgramData\Microsoft\Windows\Start Menu";

        return ForProduct(product, programFiles, commonStartMenu);
    }

    public static string WindowsCombine(params string[] parts)
        => string.Join('\\', parts.Select(part => part.Trim('\\')).Where(part => !string.IsNullOrWhiteSpace(part)));
}
