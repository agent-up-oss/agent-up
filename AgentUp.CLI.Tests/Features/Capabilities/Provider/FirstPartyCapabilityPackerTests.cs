using AgentUp.CLI.Features.Capabilities.DTOs;
using AgentUp.CLI.Features.Capabilities.Interfaces;
using AgentUp.CLI.Features.Capabilities.Models;
using AgentUp.CLI.Features.Capabilities.Providers;

namespace AgentUp.CLI.Tests.Features.Capabilities.Provider;

[TestFixture]
public sealed class FirstPartyCapabilityPackerTests
{
    [Test]
    public async Task PackFirstParty_runsThePackScriptAgainstTheResolvedRegistry()
    {
        var root = CreateRepository();
        try
        {
            var runner = new RecordingProcessRunner();
            var packer = new FirstPartyCapabilityPacker(runner, () => Path.Join(root, "registry"));

            var packed = await packer.PackFirstPartyAsync(Path.Join(root, "nested"));

            Assert.Multiple(() =>
            {
                Assert.That(packed.Succeeded, Is.True);
                Assert.That(runner.FileName, Is.EqualTo("bash"));
                Assert.That(runner.Arguments[0], Is.EqualTo(Path.Join(root, FirstPartyCapabilities.PackScriptRelativePath)));
                Assert.That(runner.Arguments[1], Does.EndWith("registry"));
                Assert.That(runner.WorkingDirectory, Is.EqualTo(root));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task PackFirstParty_reportsANonZeroPackExit()
    {
        var root = CreateRepository();
        try
        {
            var packed = await new FirstPartyCapabilityPacker(
                new RecordingProcessRunner(exitCode: 1, stderr: "nix missing"),
                () => Path.Join(root, "registry")).PackFirstPartyAsync(root);

            Assert.Multiple(() =>
            {
                Assert.That(packed.Succeeded, Is.False);
                Assert.That(packed.Message, Does.Contain("Packing first-party capabilities failed"));
                Assert.That(packed.Message, Does.Contain("nix missing"));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task PackFirstParty_reportsAProcessLaunchFailure()
    {
        var root = CreateRepository();
        try
        {
            var packed = await new FirstPartyCapabilityPacker(
                new FailingProcessRunner(),
                () => Path.Join(root, "registry")).PackFirstPartyAsync(root);

            Assert.Multiple(() =>
            {
                Assert.That(packed.Succeeded, Is.False);
                Assert.That(packed.Message, Does.Contain("Failed to pack capabilities"));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task PackFirstParty_explainsWhenTheRepositoryIsMissing()
    {
        var empty = Directory.CreateTempSubdirectory("AgentUp-NoRepo").FullName;
        try
        {
            var packed = await new FirstPartyCapabilityPacker(new RecordingProcessRunner()).PackFirstPartyAsync(empty);

            Assert.Multiple(() =>
            {
                Assert.That(packed.Succeeded, Is.False);
                Assert.That(packed.Message, Does.Contain(FirstPartyCapabilities.PackScriptRelativePath));
            });
        }
        finally
        {
            Directory.Delete(empty, recursive: true);
        }
    }

    private static string CreateRepository()
    {
        var root = Directory.CreateTempSubdirectory("AgentUp-Repo").FullName;
        var script = Path.Join(root, FirstPartyCapabilities.PackScriptRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(script)!);
        File.WriteAllText(script, "#!/usr/bin/env bash\n");
        return root;
    }

    private sealed class RecordingProcessRunner(int exitCode = 0, string stderr = "") : ICapabilityPackProcessRunner
    {
        public string? FileName { get; private set; }

        public IReadOnlyList<string> Arguments { get; private set; } = [];

        public string? WorkingDirectory { get; private set; }

        public Task<CapabilityProcessResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            CancellationToken cancellationToken = default)
        {
            FileName = fileName;
            Arguments = arguments;
            WorkingDirectory = workingDirectory;
            return Task.FromResult(new CapabilityProcessResult(
                exitCode,
                exitCode == 0 ? $"Packed first-party capabilities into {arguments[1]}" : "",
                stderr));
        }
    }

    private sealed class FailingProcessRunner : ICapabilityPackProcessRunner
    {
        public Task<CapabilityProcessResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("bash is not allowlisted.");
    }
}
