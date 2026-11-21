using AgentUp.TestAgents.Features.Inference.Services;

namespace AgentUp.TestAgents.Features.Inference.Controllers;

/// <summary>The inference slice's HTTP boundary: the in-process model served as an OpenAI-compatible API.</summary>
public sealed class InferenceHttpController(OpenAiCompatibleModelService model)
{
    public Task ServeAsync(int port, Action<int> listening, CancellationToken cancellationToken) =>
        model.ServeAsync(port, null, listening, cancellationToken);

    /// <summary>Serves the model, refusing any request that does not carry <paramref name="apiKey"/> as a bearer token.</summary>
    public Task ServeAsync(int port, string? apiKey, Action<int> listening, CancellationToken cancellationToken) =>
        model.ServeAsync(port, apiKey, listening, cancellationToken);
}
