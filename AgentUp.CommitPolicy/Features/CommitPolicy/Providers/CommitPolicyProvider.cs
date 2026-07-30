namespace AgentUp.CommitPolicy.Features.CommitPolicy.Providers;

public sealed class CommitPolicyProvider
{
    public void Validate(string slice, string message, IReadOnlyList<string> files)
    {
        var commit = ConventionalCommitParts(message);
        if (commit is null)
            throw new InvalidOperationException("Commit message must start with one of: feat, fix, test, chore, refactor, style, docs.");

        var prefix = commit.Value.Prefix;
        if (string.IsNullOrWhiteSpace(commit.Value.Scope))
            throw new InvalidOperationException("Commit message must include a scope matching the queued slice, such as fix(Commits): validate queue metadata.");

        if (NormalizeSliceName(commit.Value.Scope) != NormalizeSliceName(slice))
            throw new InvalidOperationException($"Commit message scope '{commit.Value.Scope}' does not match queued slice '{slice}'.");

        if (prefix == "docs" && files.Any(file => !IsDocumentationFile(file)))
            throw new InvalidOperationException("docs commits may only include documentation files such as docs/*, README*, AGENTS.md, CONTRIBUTING.md, SECURITY.md, CODE_OF_CONDUCT.md, or CHANGELOG.md.");

        if (prefix == "style" && files.Any(file => !IsStyleFile(file)))
            throw new InvalidOperationException("style commits may only include CSS or HTML files.");

        if (prefix == "test" && files.Any(file => !IsTestOrPackageSmokeFile(file)))
            throw new InvalidOperationException("test commits may only include test or smoke-validation files. Use fix or feat when production changes and same-slice tests are queued together.");

        if (prefix == "chore" && files.Any(file => !IsMaintenanceFile(file)))
            throw new InvalidOperationException("chore commits may only include maintenance, packaging, CI, or tool configuration files. Use fix, feat, or refactor for source/runtime behavior changes.");

        ValidateSliceBoundary(slice, files);
    }

    private static (string Prefix, string? Scope)? ConventionalCommitParts(string message)
    {
        var separator = message.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0)
            return null;

        var type = message[..separator];
        var scopeStart = type.IndexOf('(', StringComparison.Ordinal);
        string? scope = null;
        if (scopeStart >= 0)
        {
            var scopeEnd = type.IndexOf(')', scopeStart + 1);
            if (scopeEnd <= scopeStart + 1)
                return null;
            scope = type[(scopeStart + 1)..scopeEnd];
            type = type[..scopeStart];
        }

        if (type.EndsWith('!'))
            type = type[..^1];

        return type is "feat" or "fix" or "test" or "chore" or "refactor" or "style" or "docs"
            ? (type, scope)
            : null;
    }

    private static bool IsDocumentationFile(string path)
    {
        var normalized = path.Replace('\\', '/');
        var fileName = Path.GetFileName(normalized);
        return normalized.StartsWith("docs/", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("README", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "AGENTS.md", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "CONTRIBUTING.md", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "SECURITY.md", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "CODE_OF_CONDUCT.md", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "CHANGELOG.md", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStyleFile(string path)
    {
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".css", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".html", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".htm", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTestOrPackageSmokeFile(string path)
    {
        var normalized = path.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var fileName = Path.GetFileName(normalized);
        return segments.Any(segment =>
                segment.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase)
                || string.Equals(segment, "tests", StringComparison.OrdinalIgnoreCase)
                || string.Equals(segment, "test", StringComparison.OrdinalIgnoreCase)
                || string.Equals(segment, "spec", StringComparison.OrdinalIgnoreCase)
                || string.Equals(segment, "specs", StringComparison.OrdinalIgnoreCase)
                || segment.Contains("PackageSmoke", StringComparison.OrdinalIgnoreCase))
            || fileName.Contains("Tests.", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("Test.", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains(".spec.", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains(".test.", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMaintenanceFile(string path)
    {
        var normalized = path.Replace('\\', '/');
        var root = RootDirectory(normalized);
        var fileName = Path.GetFileName(normalized);
        var extension = Path.GetExtension(fileName);
        return normalized.StartsWith(".github/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(".devcontainer/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(".vscode/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("packaging/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("scripts/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("tools/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(root, "packaging", StringComparison.OrdinalIgnoreCase)
            || string.Equals(root, "scripts", StringComparison.OrdinalIgnoreCase)
            || string.Equals(root, "tools", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "Dockerfile", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "docker-compose.yml", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "docker-compose.yaml", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "global.json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "nuget.config", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("Directory.Build.", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("Directory.Packages.", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".props", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".targets", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".editorconfig", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateSliceBoundary(string slice, IReadOnlyList<string> files)
    {
        var featureSlices = files
            .Select(FeatureSlice)
            .Where(featureSlice => featureSlice is not null)
            .Distinct(StringComparer.Ordinal)
            .Cast<string>()
            .ToList();
        if (featureSlices.Count == 0)
            return;
        if (featureSlices.Count > 1)
            throw new InvalidOperationException($"Commit spans multiple feature slices: {string.Join(", ", featureSlices)}. Create one queued commit per slice.");

        var expected = NormalizeSliceName(featureSlices[0]);
        var actual = NormalizeSliceName(slice);
        if (actual != expected)
            throw new InvalidOperationException($"Commit slice '{slice}' does not match feature slice '{featureSlices[0]}'.");
    }

    private static string? FeatureSlice(string path)
    {
        var parts = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i + 1 < parts.Length; i++)
        {
            if (parts[i] == "Features")
                return parts[i + 1];
        }

        return null;
    }

    private static string NormalizeSliceName(string value)
    {
        var last = value.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? value;
        return string.Concat(last.Where(char.IsLetterOrDigit)).ToLowerInvariant();
    }

    private static string RootDirectory(string path)
        => path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
}
