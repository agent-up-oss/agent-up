namespace AgentUp.TestAgents.Features.Inference.Models;

/// <summary>
/// The pinned CPU GGUF the locally-inferring test agent loads. Weights are fetched from the
/// documented mirror on a cache miss; a SHA mismatch is a hard failure, not a retry loop.
/// </summary>
public sealed record GgufModelSpec(
    string Id,
    string FileName,
    string Sha256,
    string SourceUrl,
    uint ContextSize)
{
    /// <summary>
    /// Qwen2.5-1.5B-Instruct Q4_K_M. Spike on 12 Oct 2026: 8/8 parseable tagged tool calls
    /// on the two single-step scenarios, ~1.5s per generation on 4 CPU threads.
    /// </summary>
    public static GgufModelSpec Qwen25Instruct15B { get; } = new(
        "qwen2.5-1.5b-instruct-q4-k-m",
        "qwen2.5-1.5b-instruct-q4_k_m.gguf",
        "6a1a2eb6d15622bf3c96857206351ba97e1af16c30d7a74ee38970e434e9407e",
        "https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF/resolve/main/qwen2.5-1.5b-instruct-q4_k_m.gguf",
        2048);
}
