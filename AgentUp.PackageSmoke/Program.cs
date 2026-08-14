using AgentUp.CLI;
using AgentUp.CLI.Composition;
using AgentUp.Desktop;
using AgentUp.Desktop.Composition;
using AgentUp.InstallerApp.Composition;
using LocalInstaller.App;
using LocalInstaller.App.Composition;
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
    .ServerUrlEnvironmentVariable("AGENTUP_SERVER_URL")
    .RunAsync(Console.Out, Console.Error);
