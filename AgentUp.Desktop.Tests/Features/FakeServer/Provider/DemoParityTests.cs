using System.Text.Json;
using System.Text.Json.Nodes;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.FakeServer.Provider;

/// <summary>
/// Desktop's Demo held to AgentUp.FakeServer/parity.json, the client contract the real Server and
/// Mobile's Demo answer too. The Backend · Demo (Desktop) leg of CI runs this fixture. The rules are
/// the ones AgentUp.FakeServer/parity.mjs states for the other two legs: a shared read answers with
/// the Server's status, body kind and keys, less and plus the differences the manifest declares,
/// and an operation the manifest calls Server-only is one the Demo does not answer.
/// </summary>
[TestFixture]
[Category("DemoParity")]
public sealed class DemoParityTests
{
    private static readonly string[] Methods = ["get", "post", "put", "delete", "patch"];

    private static readonly Dictionary<string, string> DefaultParams = new(StringComparer.Ordinal)
    {
        ["workspaceId"] = "workspace",
        ["id"] = "workspace",
        ["application"] = "application",
        ["name"] = "application",
        ["sessionId"] = "session",
        ["evidenceId"] = "evidence",
        ["owner"] = "owner",
        ["repository"] = "repository"
    };

    [Test]
    public void Manifest_classifiesEveryOperationInTheApiReferenceOnce()
    {
        var manifest = Read("AgentUp.FakeServer", "parity.json");
        var documented = Operations(Read("docs", "api-reference", "openapi.json"));
        var listed = Entries(manifest).Select(Route).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(listed, Is.Unique);
            Assert.That(listed.Order(StringComparer.Ordinal), Is.EqualTo(documented));
        });
    }

    [Test]
    public void Demo_answersEveryReadTheManifestSaysItShares_asRecorded()
    {
        var manifest = Read("AgentUp.FakeServer", "parity.json");

        var problems = Entries(manifest)
            .Where(entry => Route(entry).StartsWith("GET ", StringComparison.Ordinal) && !IsAbsent(entry))
            .SelectMany(entry => Compare(Route(entry), ExpectedDemo(entry), Answer(manifest, entry)))
            .ToArray();

        Assert.That(problems, Is.Empty);
    }

    [Test]
    public void Demo_answersNothingTheManifestSaysOnlyARealServerAnswers()
    {
        var manifest = Read("AgentUp.FakeServer", "parity.json");

        var answered = Entries(manifest)
            .Where(IsAbsent)
            .Select(entry => (Route: Route(entry), Answer(manifest, entry).Status))
            .Where(result => result.Status != 404)
            .Select(result => $"{result.Route} answered {result.Status}; mark it as a Demo answer in parity.json.")
            .ToArray();

        Assert.That(answered, Is.Empty);
    }

    private static (int Status, string Body, string[]? Keys) Answer(JsonNode manifest, JsonNode entry)
    {
        var route = Route(entry);
        var method = route[..route.IndexOf(' ', StringComparison.Ordinal)];
        var (path, query) = Concrete(entry, manifest["fixtures"]!["demo"]!.AsObject());
        var request = new FakeBackendRequestDtoBuilder()
            .Send(method, path)
            .WithQuery(method == "GET" ? query : "")
            .Build();
        var response = FakeServerTestComposition.Backend((_, _) => { }).Handle(request);
        var text = response.Body ?? "";
        var body = response.ContentType.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase)
            ? "stream"
            : BodyKind(response.ContentType, text);
        return (response.Status, body, body == "object" ? KeysOf(text) : null);
    }

    private static (int Status, string? Body, string[]? Keys) ExpectedDemo(JsonNode entry)
    {
        var demo = entry["demo"]!;
        var server = entry["server"];
        var keys = Strings(demo["keys"]);
        if (keys is null && Strings(server?["keys"]) is { } serverKeys)
        {
            var omits = Strings(demo["omits"]) ?? [];
            keys = serverKeys.Where(key => !omits.Contains(key, StringComparer.Ordinal))
                .Concat(Strings(demo["extra"]) ?? [])
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

        return (demo["status"]!.GetValue<int>(), demo["body"]?.GetValue<string>() ?? server?["body"]?.GetValue<string>(), keys);
    }

    private static IEnumerable<string> Compare(
        string route,
        (int Status, string? Body, string[]? Keys) expected,
        (int Status, string Body, string[]? Keys) observed)
    {
        if (expected.Status != observed.Status)
            yield return $"{route} answered {observed.Status}, expected {expected.Status}.";
        if (expected.Body is not null && expected.Body != observed.Body)
            yield return $"{route} answered a {observed.Body} body, expected {expected.Body}.";
        if (expected.Keys is null || observed.Keys is null)
            yield break;
        var missing = expected.Keys.Except(observed.Keys, StringComparer.Ordinal).ToArray();
        var extra = observed.Keys.Except(expected.Keys, StringComparer.Ordinal).ToArray();
        if (missing.Length > 0)
            yield return $"{route} is missing {string.Join(", ", missing)}.";
        if (extra.Length > 0)
            yield return $"{route} also returns {string.Join(", ", extra)}, which the manifest does not record.";
    }

    private static (string Path, string Query) Concrete(JsonNode entry, JsonObject fixture)
    {
        var route = Route(entry);
        var template = route[(route.IndexOf(' ', StringComparison.Ordinal) + 1)..];
        var parameters = new Dictionary<string, string>(DefaultParams, StringComparer.Ordinal);
        foreach (var pair in entry["params"]?.AsObject() ?? [])
            parameters[pair.Key] = pair.Value!.GetValue<string>();

        var path = System.Text.RegularExpressions.Regex.Replace(template, "\\{([^}]+)\\}", match =>
            Uri.EscapeDataString(Fixture(fixture, parameters[match.Groups[1].Value]) ?? "absent"));
        var query = string.Join('&', (entry["query"]?.AsObject() ?? []).Select(pair =>
        {
            var token = pair.Value!.GetValue<string>();
            return $"{pair.Key}={Uri.EscapeDataString(token.Length == 0 ? "" : Fixture(fixture, token) ?? "")}";
        }));
        return (path, query.Length == 0 ? "" : $"?{query}");
    }

    private static string BodyKind(string contentType, string text)
    {
        var type = contentType.ToLowerInvariant();
        if (type.Contains("text/event-stream", StringComparison.Ordinal))
            return "stream";
        if (type.Contains("json", StringComparison.Ordinal) && !type.Contains("ndjson", StringComparison.Ordinal) && !type.Contains("jsonl", StringComparison.Ordinal))
        {
            try
            {
                switch (JsonNode.Parse(text))
                {
                    case JsonArray:
                        return "array";
                    case JsonObject:
                        return "object";
                }
            }
            catch (JsonException)
            {
                return "text";
            }
        }

        return text.Length == 0 ? "empty" : "text";
    }

    private static string[] KeysOf(string text)
        => JsonNode.Parse(text)!.AsObject().Select(pair => pair.Key).Order(StringComparer.Ordinal).ToArray();

    private static string[] Operations(JsonNode openapi)
        => openapi["paths"]!.AsObject()
            .SelectMany(path => Methods
                .Where(method => path.Value!.AsObject().ContainsKey(method))
                .Select(method => $"{method.ToUpperInvariant()} {path.Key}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static IEnumerable<JsonNode> Entries(JsonNode manifest) => manifest["operations"]!.AsArray().Select(entry => entry!);

    private static string Route(JsonNode entry) => entry["route"]!.GetValue<string>();

    private static bool IsAbsent(JsonNode entry) => entry["demo"] is JsonObject demo && demo.ContainsKey("absent");

    private static string? Fixture(JsonObject fixture, string token) => fixture[token]?.GetValue<string>();

    private static string[]? Strings(JsonNode? node) => node?.AsArray().Select(item => item!.GetValue<string>()).ToArray();

    private static JsonNode Read(params string[] parts)
    {
        var path = Path.Join([FindRepositoryRoot(), .. parts]);
        return JsonNode.Parse(File.ReadAllText(path))!;
    }

    private static string FindRepositoryRoot()
    {
        var directory = TestContext.CurrentContext.TestDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Join(directory, "agent-up.sln")))
                return directory;

            var parent = Directory.GetParent(directory)?.FullName;
            if (parent == directory)
                break;

            directory = parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
