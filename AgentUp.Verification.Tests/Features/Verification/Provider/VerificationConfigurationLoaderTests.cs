using AgentUp.Verification.Features.Verification.Models;
using AgentUp.Verification.Features.Verification.Providers;

namespace AgentUp.Verification.Tests.Features.Verification.Provider;

[TestFixture]
public sealed class VerificationConfigurationLoaderTests
{
    private static string WriteRepository(string agentUpJson)
    {
        var root = Path.Join(
            TestContext.CurrentContext.WorkDirectory,
            "verification-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Join(root, "agent-up.json"), agentUpJson);
        return root;
    }

    [Test]
    public void Load_returnsEmptyWhenTheRepositoryHasNoAgentUpJson()
    {
        var root = Path.Join(TestContext.CurrentContext.WorkDirectory, "absent-" + Guid.NewGuid().ToString("N"));

        Assert.That(new VerificationConfigurationLoader().Load(root).IsConfigured, Is.False);
    }

    [Test]
    public void Load_returnsEmptyWhenAgentUpJsonHasNoVerificationSection()
    {
        var root = WriteRepository("""{ "name": "Agent Up" }""");

        Assert.That(new VerificationConfigurationLoader().Load(root).IsConfigured, Is.False);
    }

    [Test]
    public void Load_readsChecksPathsAlwaysAndEnforcement()
    {
        var root = WriteRepository("""
        {
          "verification": {
            "enforcement": "block",
            "always": ["architecture"],
            "checks": {
              "architecture": { "command": "dotnet test Arch", "tier": "fast" },
              "linux-smoke": {
                "command": "./smoke.sh",
                "tier": "platform",
                "platforms": ["linux"],
                "ciOnly": true,
                "inputs": ["packaging"],
                "workingDirectory": "tools"
              }
            },
            "paths": [ { "match": "packaging/**", "checks": ["linux-smoke"] } ]
          }
        }
        """);

        var configuration = new VerificationConfigurationLoader().Load(root);
        var smoke = configuration.Checks["linux-smoke"];

        Assert.Multiple(() =>
        {
            Assert.That(configuration.Enforcement, Is.EqualTo(VerificationEnforcement.Block));
            Assert.That(configuration.Always, Is.EqualTo(new[] { "architecture" }));
            Assert.That(configuration.Paths.Single().Match, Is.EqualTo("packaging/**"));
            Assert.That(smoke.Tier, Is.EqualTo(CheckTier.Platform));
            Assert.That(smoke.Platforms, Is.EqualTo(new[] { "linux" }));
            Assert.That(smoke.CiOnly, Is.True);
            Assert.That(smoke.Inputs, Is.EqualTo(new[] { "packaging" }));
        });
    }

    [Test]
    public void Load_defaultsEnforcementToWarnSoAnIncompleteMapDoesNotBlockWork()
    {
        var root = WriteRepository("""
        {
          "verification": {
            "checks": { "build": { "command": "dotnet build" } },
            "paths": [ { "match": "**", "checks": ["build"] } ]
          }
        }
        """);

        Assert.That(new VerificationConfigurationLoader().Load(root).Enforcement,
            Is.EqualTo(VerificationEnforcement.Warn));
    }

    [Test]
    public void Load_throwsOnMalformedJsonInsteadOfSilentlyDisablingTheGate()
    {
        var root = WriteRepository("""{ "verification": { "checks": { """);

        Assert.That(() => new VerificationConfigurationLoader().Load(root),
            Throws.TypeOf<VerificationConfigurationException>());
    }

    [Test]
    public void Load_throwsWhenAPathRuleReferencesAnUnknownCheck()
    {
        var root = WriteRepository("""
        {
          "verification": {
            "checks": { "build": { "command": "dotnet build" } },
            "paths": [ { "match": "AgentUp.Server/**", "checks": ["server-unit"] } ]
          }
        }
        """);

        Assert.That(() => new VerificationConfigurationLoader().Load(root),
            Throws.TypeOf<VerificationConfigurationException>()
                .With.Message.Contains("server-unit"));
    }

    [Test]
    public void Load_throwsWhenAlwaysReferencesAnUnknownCheck()
    {
        var root = WriteRepository("""
        {
          "verification": {
            "always": ["nope"],
            "checks": { "build": { "command": "dotnet build" } },
            "paths": []
          }
        }
        """);

        Assert.That(() => new VerificationConfigurationLoader().Load(root),
            Throws.TypeOf<VerificationConfigurationException>().With.Message.Contains("nope"));
    }

    [Test]
    public void Load_throwsWhenACheckHasNoCommand()
    {
        var root = WriteRepository("""
        {
          "verification": {
            "checks": { "build": { "tier": "fast" } },
            "paths": []
          }
        }
        """);

        Assert.That(() => new VerificationConfigurationLoader().Load(root),
            Throws.TypeOf<VerificationConfigurationException>().With.Message.Contains("command"));
    }

    [Test]
    public void Load_throwsOnAnUnknownTier()
    {
        var root = WriteRepository("""
        {
          "verification": {
            "checks": { "build": { "command": "dotnet build", "tier": "immediate" } },
            "paths": []
          }
        }
        """);

        Assert.That(() => new VerificationConfigurationLoader().Load(root),
            Throws.TypeOf<VerificationConfigurationException>().With.Message.Contains("immediate"));
    }

    [Test]
    public void Load_throwsOnAnUnknownPlatform()
    {
        var root = WriteRepository("""
        {
          "verification": {
            "checks": { "build": { "command": "dotnet build", "platforms": ["solaris"] } },
            "paths": []
          }
        }
        """);

        Assert.That(() => new VerificationConfigurationLoader().Load(root),
            Throws.TypeOf<VerificationConfigurationException>().With.Message.Contains("solaris"));
    }

    [Test]
    public void Load_throwsWhenAPathRuleOmitsItsChecksArray()
    {
        var root = WriteRepository("""
        {
          "verification": {
            "checks": { "build": { "command": "dotnet build" } },
            "paths": [ { "match": "docs/**" } ]
          }
        }
        """);

        Assert.That(() => new VerificationConfigurationLoader().Load(root),
            Throws.TypeOf<VerificationConfigurationException>().With.Message.Contains("docs/**"));
    }

    [Test]
    public void Load_throwsWhenTheSectionOmitsChecks()
    {
        var root = WriteRepository("""{ "verification": { "paths": [] } }""");

        Assert.That(() => new VerificationConfigurationLoader().Load(root),
            Throws.TypeOf<VerificationConfigurationException>().With.Message.Contains("checks"));
    }
}
