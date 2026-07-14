using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using AgentUp.Fixtures;
using NUnitLite;

namespace AgentUp.Tests;

public static class E2ETestRunner
{
    public static int Main(string[] args)
    {
        if (OperatingSystem.IsMacOS())
            return RunMacOs(args);

        return RunTests(args);
    }

    private static int RunMacOs(string[] args)
    {
        var lifetime = new ClassicDesktopStyleApplicationLifetime
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown
        };

        DesktopFixtureAdapter.ConfigureAvalonia<E2ETestApp>()
            .SetupWithLifetime(lifetime);
        DesktopFixtureHost.MarkAvaloniaReady();

        var exitCode = 1;
        Exception? testException = null;
        var testsFinished = new ManualResetEventSlim(false);

        var testThread = new Thread(() =>
        {
            try
            {
                exitCode = RunTests(args);
            }
            catch (Exception ex)
            {
                testException = ex;
            }
            finally
            {
                testsFinished.Set();
                Dispatcher.UIThread.Post(() =>
                {
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime currentLifetime)
                        currentLifetime.Shutdown();
                    else
                        Dispatcher.UIThread.InvokeShutdown();
                });
            }
        })
        {
            IsBackground = true,
            Name = "Agent-Up E2E NUnitLite"
        };

        testThread.Start();
        Dispatcher.UIThread.MainLoop(CancellationToken.None);
        testsFinished.Wait(TimeSpan.FromSeconds(10));

        if (testException is not null)
            throw testException;

        return exitCode;
    }

    private static int RunTests(string[] args)
    {
        return new AutoRun(typeof(E2ETestRunner).Assembly)
            .Execute(args.Length > 0 ? args : ["--workers=0"]);
    }
}
