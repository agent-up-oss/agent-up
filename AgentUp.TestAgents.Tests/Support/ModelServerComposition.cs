using AgentUp.TestAgents.Features.Inference.Controllers;
using AgentUp.TestAgents.Features.Inference.Providers;
using AgentUp.TestAgents.Features.Inference.Services;

namespace AgentUp.TestAgents.Tests.Support;

/// <summary>Composes the model server over a scripted generator, so no test loads a GGUF.</summary>
internal static class ModelServerComposition
{
    public static OpenAiCompatibleModelService Service(ScriptedTokenGenerator generator) => new(
        generator,
        new ChatPromptProvider(),
        new ChatCompletionReader(),
        new ChatCompletionWriter(),
        new FixedLoopInferenceSettings(42),
        new ModelHttpListener());

    public static InferenceHttpController Controller(ScriptedTokenGenerator generator)
        => new(Service(generator));
}
