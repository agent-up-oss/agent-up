using AgentUp.CLI;
using AgentUp.CLI.Composition;
using AgentUp.Desktop;
using AgentUp.Desktop.Composition;
using AgentUp.InstallerApp;
using AgentUp.InstallerApp.Composition;
using AgentUp.Server;
using AgentUp.Server.Composition;
using AgentUp.Tray;
using AgentUp.Tray.Composition;
using LocalInstaller.Smoke.Composition;

return await LocalInstallerSmoke.Create(args)
    .UseProductManifest<AgentUpProductManifest>()
    .InstallerApplication<AgentUpInstallerAppManifest>()
    .InstallerOptionCli<AgentUpCliManifest>()
    .InstallerOptionServer<AgentUpServerManifest>()
    .InstallerOptionDesktop<AgentUpDesktopManifest>()
    .InstallerOptionTray<AgentUpTrayManifest>()
    .WorkspaceConfigFileName("agent-up.json")
    .RunAsync(Console.Out, Console.Error);
