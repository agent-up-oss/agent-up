using System.Diagnostics;
using System.Globalization;

namespace AgentUp.Server.Features.Metrics.Providers;

public static class ProcessMetricsSampler
{
    public static async Task<IReadOnlyDictionary<string, string>> SampleAsync(
        TimeSpan cpuSampleWindow,
        CancellationToken cancellationToken)
    {
        using var process = Process.GetCurrentProcess();
        process.Refresh();

        var startCpu = process.TotalProcessorTime;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await Task.Delay(cpuSampleWindow, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }

        process.Refresh();
        var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
        var cpuDeltaMs = (process.TotalProcessorTime - startCpu).TotalMilliseconds;
        var cpuPercent = elapsedMs <= 0
            ? 0
            : cpuDeltaMs / (elapsedMs * Environment.ProcessorCount) * 100;

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["process.cpuPercent"] = cpuPercent.ToString("F1", CultureInfo.InvariantCulture),
            ["process.workingSetBytes"] = process.WorkingSet64.ToString(CultureInfo.InvariantCulture),
            ["process.privateMemoryBytes"] = process.PrivateMemorySize64.ToString(CultureInfo.InvariantCulture),
            ["process.threadCount"] = process.Threads.Count.ToString(CultureInfo.InvariantCulture),
            ["process.handleCount"] = process.HandleCount.ToString(CultureInfo.InvariantCulture),
            ["process.processorCount"] = Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture),
            ["process.gcHeapBytes"] = GC.GetTotalMemory(false).ToString(CultureInfo.InvariantCulture)
        };
    }
}
