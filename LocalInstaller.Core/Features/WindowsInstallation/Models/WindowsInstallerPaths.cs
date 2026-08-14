using LocalInstaller.Core.Features.Installation.Models;

namespace LocalInstaller.Core.Features.WindowsInstallation.Models;

public sealed partial record WindowsInstallerPaths(
    string RootDirectory,
    string DesktopDirectory,
    string ServerDirectory,
    string CliDirectory,
    string TrayDirectory,
    string BinDirectory,
    string StartMenuShortcutPath,
    string CliShimName,
    string UninstallScriptName,
    string DesktopExecutableName = "desktop.exe",
    string ServerExecutableName = "server.exe",
    string TrayExecutableName = "tray.exe",
    string CliExecutableName = "cli.exe")
{
    public string DesktopExecutable => WindowsCombine(DesktopDirectory, DesktopExecutableName);
    public string ServerExecutable => WindowsCombine(ServerDirectory, ServerExecutableName);
    public string TrayExecutable => WindowsCombine(TrayDirectory, TrayExecutableName);
    public string CliExecutable => WindowsCombine(CliDirectory, CliExecutableName);
    public string CliShimPath => CliShimPathFor(CliShimName);
    public string CliShimPathFor(string cliShimName)
        => WindowsCombine(BinDirectory, WindowsInstallerManifest.RequireSafeCliShimFileName(cliShimName));
    public string UninstallScriptPath => WindowsCombine(RootDirectory, UninstallScriptName);

    public static WindowsInstallerPaths ForProduct(
        ProductManifest product,
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
            UninstallScriptName: $"uninstall-{product.Slug}.ps1",
            DesktopExecutableName: ExecutableName(product, InstallerComponentTarget.Desktop, "desktop"),
            ServerExecutableName: ExecutableName(product, InstallerComponentTarget.Server, "server"),
            TrayExecutableName: ExecutableName(product, InstallerComponentTarget.Tray, "tray"),
            CliExecutableName: ExecutableName(product, InstallerComponentTarget.Cli, "cli"));
    }

    public static WindowsInstallerPaths ForProduct(ProductManifest product)
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

    private static string ExecutableName(ProductManifest product, InstallerComponentTarget target, string fallback)
    {
        var name = product.InstallableComponents.FirstOrDefault(component => component.Target == target)?.ExecutableName ?? fallback;
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name : name + ".exe";
    }
}
