using System.Text.Json;
using AgentUp.Architecture.Tests.Fixtures;
using YamlDotNet.RepresentationModel;

namespace AgentUp.Architecture.Tests.Rules;

/// <summary>
/// Keeps .github/workflows/ci.yml and .github/behavior-matrix.json describing one pipeline. Each
/// stage is one matrix job named after its domain, every matrix entry is a declared leg that records
/// its verdict last, every declared leg is a matrix entry, the Behavior matrix job waits for all of
/// them and Release waits for it, and Source gates release only - no behavior leg waits for a source
/// suite or for coverage. Definition: docs/developer-guide/repo/ci.md.
/// </summary>
[TestFixture]
public sealed class CiBehaviorMatrix
{
    private const string VerdictAction = "./.github/actions/record-verdict";
    private const string Gate = "behavior-matrix";
    private const string Release = "release";
    private static readonly string[] StructuralJobs = ["version", Gate, Release];

    [Test]
    public void EveryCiJobIsADeclaredLeg_andEveryLegIsAJob()
    {
        var jobs = Jobs();
        var legs = Legs();
        var declared = legs.Select(leg => leg.Job).ToHashSet(StringComparer.Ordinal);
        var violations = new List<string>();

        violations.AddRange(JobNames(jobs)
            .Where(job => !StructuralJobs.Contains(job, StringComparer.Ordinal) && !declared.Contains(job))
            .Select(job => $"ci.yml job '{job}' is not a leg of .github/behavior-matrix.json."));
        violations.AddRange(legs
            .Where(leg => !jobs.Children.ContainsKey(new YamlScalarNode(leg.Job)))
            .Select(leg => $"Leg '{leg.Stage}/{leg.Id}' names job '{leg.Job}', which ci.yml does not define."));
        var matrixJobs = legs.Where(leg => leg.Matrix).Select(leg => leg.Job).Distinct(StringComparer.Ordinal)
            .Where(job => jobs.Children.ContainsKey(new YamlScalarNode(job)));
        foreach (var job in matrixJobs)
        {
            var included = MatrixLegs((YamlMappingNode)jobs.Children[new YamlScalarNode(job)]);
            var expected = legs.Where(leg => leg.Job == job).Select(leg => leg.Id).ToHashSet(StringComparer.Ordinal);
            violations.AddRange(expected.Except(included).Select(id => $"Matrix job '{job}' has no include entry with leg '{id}'."));
            violations.AddRange(included.Except(expected).Select(id => $"Matrix job '{job}' runs leg '{id}', which behavior-matrix.json does not declare."));
        }

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void EveryStageIsOneMatrixJobNamedAfterItsDomain()
    {
        var violations = Legs().GroupBy(leg => leg.Stage, StringComparer.Ordinal)
            .Where(stage => stage.Any(leg => !leg.Matrix || leg.Job != stage.Key))
            .Select(stage => $"Stage '{stage.Key}' must run as the one matrix job '{stage.Key}', with each leg as a matrix entry.")
            .ToArray();

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void EveryLegRecordsItsVerdictLast_evenWhenItFailed()
    {
        var jobs = Jobs();
        var violations = new List<string>();
        var groups = Legs().GroupBy(leg => leg.Job, StringComparer.Ordinal)
            .Where(group => jobs.Children.ContainsKey(new YamlScalarNode(group.Key)));
        foreach (var group in groups)
        {
            var node = (YamlMappingNode)jobs.Children[new YamlScalarNode(group.Key)];
            var step = ((YamlSequenceNode)node.Children[new YamlScalarNode("steps")]).Children
                .OfType<YamlMappingNode>().Last();
            var with = step.Children.TryGetValue(new YamlScalarNode("with"), out var withNode) ? (YamlMappingNode)withNode : null;
            var first = group.First();
            var expectedLeg = first.Matrix ? "${{ matrix.leg }}" : first.Id;
            if (Scalar(step, "uses") != VerdictAction)
                violations.Add($"Job '{group.Key}' must end with {VerdictAction}.");
            else if (!(Scalar(step, "if") ?? string.Empty).Contains("always()", StringComparison.Ordinal))
                violations.Add($"Job '{group.Key}' records its verdict only on success; use if: ${{{{ always() }}}}.");
            else if (with is null || Scalar(with, "stage") != first.Stage || Scalar(with, "leg") != expectedLeg
                     || Scalar(with, "status") != "${{ job.status }}")
                violations.Add($"Job '{group.Key}' must record stage '{first.Stage}', leg '{expectedLeg}' and job.status.");
            if (group.Select(leg => leg.Stage).Distinct(StringComparer.Ordinal).Count() > 1)
                violations.Add($"Job '{group.Key}' runs legs of more than one stage.");
        }

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void BehaviorMatrixWaitsForEveryLeg_andReleaseWaitsForIt()
    {
        var jobs = Jobs();
        var gate = (YamlMappingNode)jobs.Children[new YamlScalarNode(Gate)];
        var gateNeeds = Needs(gate);
        var missing = Legs().Select(leg => leg.Job).Distinct(StringComparer.Ordinal)
            .Where(job => !gateNeeds.Contains(job)).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(missing, Is.Empty, "The Behavior matrix job must wait for every leg it judges.");
            Assert.That(Scalar(gate, "if"), Does.Contain("always()"), "The Behavior matrix job must run when a leg failed.");
            Assert.That(Needs((YamlMappingNode)jobs.Children[new YamlScalarNode(Release)]), Does.Contain(Gate));
        });
    }

    [Test]
    public void SourceGatesReleaseOnly()
    {
        var jobs = Jobs();
        var stageOf = Legs().GroupBy(leg => leg.Job, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Stage, StringComparer.Ordinal);

        var violations = JobNames(jobs)
            .Where(job => job is not Gate and not Release)
            .SelectMany(job => Needs((YamlMappingNode)jobs.Children[new YamlScalarNode(job)])
                .Where(need => stageOf.GetValueOrDefault(need) == "source" && stageOf.GetValueOrDefault(job) != "source")
                .Select(need => $"'{job}' waits for source job '{need}'; source suites and coverage gate release only."))
            .ToArray();

        Assert.That(violations, Is.Empty);
    }

    private static YamlMappingNode Jobs()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        using var reader = File.OpenText(Path.Join(root, ".github", "workflows", "ci.yml"));
        var workflow = new YamlStream();
        workflow.Load(reader);
        return (YamlMappingNode)((YamlMappingNode)workflow.Documents[0].RootNode).Children[new YamlScalarNode("jobs")];
    }

    private static List<(string Stage, string Id, string Job, bool Matrix)> Legs()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Join(root, ".github", "behavior-matrix.json")));
        var legs = new List<(string, string, string, bool)>();
        foreach (var stage in document.RootElement.GetProperty("stages").EnumerateArray())
        {
            var stageId = stage.GetProperty("id").GetString()!;
            var stageMatrix = stage.TryGetProperty("matrixJob", out var stageJob) ? stageJob.GetString() : null;
            foreach (var leg in stage.GetProperty("legs").EnumerateArray())
            {
                var job = leg.TryGetProperty("job", out var named) ? named.GetString() : null;
                var matrix = leg.TryGetProperty("matrixJob", out var legJob) ? legJob.GetString() : stageMatrix;
                legs.Add((stageId, leg.GetProperty("id").GetString()!, job ?? matrix!, job is null));
            }
        }

        return legs;
    }

    private static IEnumerable<string> JobNames(YamlMappingNode jobs)
        => jobs.Children.Keys.OfType<YamlScalarNode>().Select(key => key.Value!);

    private static HashSet<string> MatrixLegs(YamlMappingNode job)
    {
        if (!job.Children.TryGetValue(new YamlScalarNode("strategy"), out var strategy)
            || !((YamlMappingNode)strategy).Children.TryGetValue(new YamlScalarNode("matrix"), out var matrix)
            || !((YamlMappingNode)matrix).Children.TryGetValue(new YamlScalarNode("include"), out var include))
            return [];
        return ((YamlSequenceNode)include).Children.OfType<YamlMappingNode>()
            .Select(entry => Scalar(entry, "leg"))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string[] Needs(YamlMappingNode job)
    {
        if (!job.Children.TryGetValue(new YamlScalarNode("needs"), out var needs))
            return [];
        return needs switch
        {
            YamlScalarNode single => [single.Value ?? string.Empty],
            YamlSequenceNode many => many.Children.OfType<YamlScalarNode>().Select(value => value.Value ?? string.Empty).ToArray(),
            _ => []
        };
    }

    private static string? Scalar(YamlMappingNode node, string key)
        => node.Children.TryGetValue(new YamlScalarNode(key), out var value) ? (value as YamlScalarNode)?.Value : null;
}
