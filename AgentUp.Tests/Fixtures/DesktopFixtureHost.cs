using System.Threading;
using AgentUp.Fixtures;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;
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
        _adapter = DesktopFixtureAdapter.Create();
        _adapter.SetUp();
        StartAvalonia(_adapter);
    }

    internal static void MarkAvaloniaReady() => AvaloniaReady.Set();

    private static void StartAvalonia(IDesktopFixtureAdapter adapter)
    {
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

        if (!AvaloniaReady.Wait(TimeSpan.FromSeconds(60)))
            throw new TimeoutException($"Avalonia platform failed to initialize through {adapter.Name} within 60 seconds. {adapter.StartupFailureHint}");
    }

    [OneTimeTearDown]
    public void Stop()
    {
        try
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lt)
                    lt.Shutdown();
            }).GetAwaiter().GetResult();
        }
        catch (TaskCanceledException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        _adapter?.Dispose();
    }
}

file sealed class E2ETestApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
}
