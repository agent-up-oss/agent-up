using AgentUp.Desktop.Features.FirstRun.Services;
using AgentUp.Tests.Fixtures;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace AgentUp.Tests;

[SetUpFixture]
public sealed class DesktopFixtureHost
{
    private static readonly ManualResetEventSlim AvaloniaReady = new(false);
    private IDesktopFixtureAdapter? _adapter;

    [OneTimeSetUp]
    public void Start()
    {
        Environment.SetEnvironmentVariable(FileFirstRunTutorialSettingsStore.SkipTutorialEnvironmentVariable, "1");
        _adapter = DesktopFixtureAdapter.Create();
        TestContext.Progress.WriteLine($"Starting native desktop fixture through {_adapter.Name}.");
        _adapter.SetUp();
        TestContext.Progress.WriteLine($"Native desktop fixture setup completed through {_adapter.Name}.");
        StartAvalonia(_adapter);
        TestContext.Progress.WriteLine($"Avalonia platform initialized through {_adapter.Name}.");
    }

    internal static void MarkAvaloniaReady() => AvaloniaReady.Set();

    private static void StartAvalonia(IDesktopFixtureAdapter adapter)
    {
        if (adapter.RequiresSetupThreadAvalonia)
        {
            if (Application.Current is not null)
            {
                MarkAvaloniaReady();
                return;
            }

            var lifetime = new ClassicDesktopStyleApplicationLifetime
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };
            DesktopFixtureAdapter.ConfigureAvalonia<E2ETestApp>()
                .SetupWithLifetime(lifetime);

            MarkAvaloniaReady();
            return;
        }

        var thread = new Thread(() =>
        {
            DesktopFixtureAdapter.ConfigureAvalonia<E2ETestApp>()
                .StartWithClassicDesktopLifetime([], ShutdownMode.OnExplicitShutdown);
        });
        thread.IsBackground = true;
        thread.Name = $"Agent-Up E2E UI ({adapter.Name})";

        if (adapter.RequiresStaThread && OperatingSystem.IsWindows())
            thread.SetApartmentState(ApartmentState.STA);

        thread.Start();

        if (!AvaloniaReady.Wait(TimeSpan.FromSeconds(30)))
            throw new TimeoutException($"Avalonia platform failed to initialize through {adapter.Name} within 30 seconds. {adapter.StartupFailureHint}");
    }

    [OneTimeTearDown]
    public void Stop()
    {
        // On Linux, skip Avalonia/GTK shutdown entirely. Program.Main calls
        // Environment.Exit() after RunTests() returns, which bypasses unmanaged
        // GTK/WebKit cleanup that otherwise causes a SIGABRT during process teardown.
        if (!OperatingSystem.IsLinux())
        {
            try
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lt)
                        lt.Shutdown();
                }).GetAwaiter().GetResult();
            }
            catch (TaskCanceledException ex)
            {
                _ = ex;
                // Shutdown may race with Avalonia dispatcher teardown.
            }
            catch (InvalidOperationException ex)
            {
                _ = ex;
                // Shutdown may race with Avalonia dispatcher teardown.
            }
        }

        _adapter?.Dispose();
    }
}

internal sealed class E2ETestApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
}
