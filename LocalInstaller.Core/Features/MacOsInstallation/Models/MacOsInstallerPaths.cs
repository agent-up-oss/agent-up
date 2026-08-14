using LocalInstaller.Core.Features.Installation.Models;

namespace LocalInstaller.Core.Features.MacOsInstallation.Models;

public sealed partial record MacOsInstallerPaths(
    string AppBundleDirectory,
    string ApplicationSupportDirectory,
    string ServerDirectory,
    string TrayDirectory,
    string CliDirectory,
    string LaunchDaemonPath,
    string LogsDirectory,
    string CliSymlinkPath,
    string ServerSymlinkPath,
    string DesktopSymlinkPath,
    string BundleIconFile,
    string DesktopExecutableName,
    string ServerExecutableName,
    string TrayExecutableName,
    string CliExecutableName)
{
    public static MacOsInstallerPaths From(ProductManifest product)
    {
        var identity = MacOsInstallerManifest.ValidatedIdentityFrom(product);
        var appBundleDirectory = Under("/Applications", $"{identity.ProductName}.app");
        var applicationSupportDirectory = Under("/Library/Application Support", identity.ProductName);
        var serverDirectory = Under(applicationSupportDirectory, "server");
        var trayDirectory = Under(applicationSupportDirectory, "tray");
        var cliDirectory = Under("/usr/local", identity.Slug, "cli");
        var launchDaemonPath = Under("/Library/LaunchDaemons", $"dev.{identity.Slug}.server.plist");
        var logsDirectory = Under("/Library/Logs", identity.ProductName);
        var cliSymlinkPath = Under("/usr/local/bin", identity.Slug);
        var serverSymlinkPath = Under("/usr/local/bin", $"{identity.Slug}-server");
        var desktopSymlinkPath = Under("/usr/local/bin", $"{identity.Slug}-desktop");

        return new(
            AppBundleDirectory: appBundleDirectory,
            ApplicationSupportDirectory: applicationSupportDirectory,
            ServerDirectory: serverDirectory,
            TrayDirectory: trayDirectory,
            CliDirectory: cliDirectory,
            LaunchDaemonPath: launchDaemonPath,
            LogsDirectory: logsDirectory,
            CliSymlinkPath: cliSymlinkPath,
            ServerSymlinkPath: serverSymlinkPath,
            DesktopSymlinkPath: desktopSymlinkPath,
            BundleIconFile: identity.BundleIconFile,
            DesktopExecutableName: ExecutableName(product, InstallerComponentTarget.Desktop, "desktop"),
            ServerExecutableName: ExecutableName(product, InstallerComponentTarget.Server, "server"),
            TrayExecutableName: ExecutableName(product, InstallerComponentTarget.Tray, "tray"),
            CliExecutableName: ExecutableName(product, InstallerComponentTarget.Cli, "cli"));
    }

    private static string Under(string root, params string[] segments)
    {
        var path = System.IO.Path.GetFullPath(System.IO.Path.Join([root, .. segments]));
        var normalizedRoot = System.IO.Path.GetFullPath(root).TrimEnd(
            System.IO.Path.DirectorySeparatorChar,
            System.IO.Path.AltDirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;

        if (!path.StartsWith(normalizedRoot, StringComparison.Ordinal))
            throw new ArgumentException($"Resolved macOS installer path '{path}' must remain under '{root}'.");

        return path;
    }

    public string DesktopExecutable => System.IO.Path.Join(AppBundleDirectory, "Contents", "MacOS", DesktopExecutableName);
    public string ServerExecutable => System.IO.Path.Join(ServerDirectory, ServerExecutableName);
    public string TrayExecutable => System.IO.Path.Join(TrayDirectory, TrayExecutableName);
    public string CliExecutable => System.IO.Path.Join(CliDirectory, CliExecutableName);
    public string DesktopInfoPlistPath => System.IO.Path.Join(AppBundleDirectory, "Contents", "Info.plist");
    public string DesktopResourcesDirectory => System.IO.Path.Join(AppBundleDirectory, "Contents", "Resources");
    public string DesktopIconPath => System.IO.Path.Join(DesktopResourcesDirectory, BundleIconFile);

    private static string ExecutableName(ProductManifest product, InstallerComponentTarget target, string fallback)
        => product.InstallableComponents.FirstOrDefault(component => component.Target == target)?.ExecutableName ?? fallback;
}
