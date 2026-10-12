using System.Text;
using AgentUp.TestAgents.Features.Inference.Interfaces;
using AgentUp.TestAgents.Features.Inference.Models;
using LLama;
using LLama.Common;
using LLama.Sampling;

namespace AgentUp.TestAgents.Features.Inference.Providers;

/// <summary>
/// In-process llama.cpp CPU backend. LLamaSharp stays inside this provider so no shipped
/// product takes an inference dependency.
/// </summary>
public sealed class LlamaCppTokenGenerator : ITokenGenerator
{
    private readonly IGgufModelCache _cache;
    private readonly GgufModelSpec _spec;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private LLamaWeights? _weights;
    private ModelParams? _parameters;
    private StatelessExecutor? _executor;

    public LlamaCppTokenGenerator(IGgufModelCache cache, GgufModelSpec? spec = null)
    {
        _cache = cache;
        _spec = spec ?? GgufModelSpec.Qwen25Instruct15B;
    }

    public async Task<string> CompleteAsync(string prompt, uint seed, CancellationToken cancellationToken)
    {
        await EnsureAsync(cancellationToken);
        var inference = new InferenceParams
        {
            MaxTokens = 192,
            AntiPrompts = ["<|im_end|>"],
            SamplingPipeline = new DefaultSamplingPipeline
            {
                Seed = seed,
                Temperature = 0.2f
            }
        };

        var output = new StringBuilder();
        await foreach (var token in _executor!.InferAsync(prompt, inference).WithCancellation(cancellationToken))
            output.Append(token);
        return output.ToString();
    }

    private async Task EnsureAsync(CancellationToken cancellationToken)
    {
        if (_executor is not null)
            return;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_executor is not null)
                return;

            var path = await _cache.EnsureAsync(_spec, cancellationToken);
            var threads = Math.Max(1, Environment.ProcessorCount);
            _parameters = new ModelParams(path)
            {
                ContextSize = _spec.ContextSize,
                GpuLayerCount = 0,
                Threads = threads,
                BatchThreads = threads
            };
            _weights = LLamaWeights.LoadFromFile(_parameters);
            _executor = new StatelessExecutor(_weights, _parameters);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        _weights?.Dispose();
        _weights = null;
        _executor = null;
        _parameters = null;
        _gate.Release();
    }
}
