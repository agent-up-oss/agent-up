using AgentUp.TestAgents.Features.Inference.Models;
using AgentUp.TestAgents.Features.Inference.Services;

namespace AgentUp.TestAgents.Tests.Features.Inference.Unit;

[TestFixture]
public sealed class GgufModelSpecTests
{
    [Test]
    public void Qwen25Instruct15B_isPinnedBySha256AndMirror()
    {
        var spec = GgufModelSpec.Qwen25Instruct15B;

        Assert.Multiple(() =>
        {
            Assert.That(spec.Id, Is.EqualTo("qwen2.5-1.5b-instruct-q4-k-m"));
            Assert.That(spec.Sha256, Is.EqualTo("6a1a2eb6d15622bf3c96857206351ba97e1af16c30d7a74ee38970e434e9407e"));
            Assert.That(spec.SourceUrl, Does.StartWith("https://huggingface.co/"));
            Assert.That(spec.FileName, Does.EndWith(".gguf"));
            Assert.That(spec.ContextSize, Is.EqualTo(2048u));
        });
    }

    [Test]
    public void BuildPrompt_asksForASingleTaggedToolCall()
    {
        var prompt = LocalInferenceAcpService.BuildPrompt(["user:list the files"]);

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("<tool_call>"));
            Assert.That(prompt, Does.Contain("complete_task"));
            Assert.That(prompt, Does.Contain("user:list the files"));
        });
    }
}
