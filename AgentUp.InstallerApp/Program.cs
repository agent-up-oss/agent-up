using Avalonia;
using Avalonia.ReactiveUI;
using AgentUp.InstallerApp;
using AgentUp.Installers.Features.Execution;

SetBundledPayloadRoot();

AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .WithInterFont()
    .UseReactiveUI()
    .StartWithClassicDesktopLifetime(args);

static void SetBundledPayloadRoot()
{
    if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(InstallerPlatformAdapterFactory.PayloadRootVariable)))
        return;

    var payloadRoot = Path.Combine(AppContext.BaseDirectory, "payload");
    if (Directory.Exists(Path.Combine(payloadRoot, "desktop")) &&
        Directory.Exists(Path.Combine(payloadRoot, "server")) &&
        Directory.Exists(Path.Combine(payloadRoot, "cli")))
    {
        Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.PayloadRootVariable, payloadRoot);
    }
}
