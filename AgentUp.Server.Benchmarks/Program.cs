using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(
    typeof(AgentUp.Server.Benchmarks.Features.Applications.Benchmark.MetricsResponseParserBenchmarks).Assembly).Run(args);
