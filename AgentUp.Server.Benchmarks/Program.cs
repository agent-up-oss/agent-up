using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(
    typeof(AgentUp.Server.Benchmarks.Features.Agents.Benchmark.AgentEventFrameBenchmarks).Assembly).Run(args);
