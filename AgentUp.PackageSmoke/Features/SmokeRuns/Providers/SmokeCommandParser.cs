using System.Text.Json;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Models;
using AgentUp.PackageSmoke.Features.SmokeRuns.DTOs;
using AgentUp.PackageSmoke.Features.SmokeRuns.Interfaces;

namespace AgentUp.PackageSmoke.Features.SmokeRuns.Providers;

public sealed class SmokeCommandParser : ISmokeCommandParser
{
    public static readonly string Usage = "Usage: AgentUp.PackageSmoke [--product-manifest <path>] <validate-package|validate-installed-service> <platform> <runtime-id> <artifact-dir> <work-dir>"
        + Environment.NewLine
        + "   or: AgentUp.PackageSmoke [--product-manifest <path>] validate-installer-flow <platform> <work-dir> [payload-root]"
        + Environment.NewLine
        + "Product manifests use serviceName, cliShimName, artifactBaseName, displayName, installDirName, and optional workspaceConfigFileName.";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public SmokeCommandParseResult Parse(string[] args)
    {
        if (args is ["--help"] or ["-h"])
            return new SmokeCommandParseResult(null, Usage, HelpRequested: true);

        var parse = ParseOptions(args);
        if (parse.Args is null)
            return new SmokeCommandParseResult(null, Usage);

        args = parse.Args;
        var product = parse.ProductConfig;

        if (args.Length == 3 && args[0] == "validate-installer-flow")
            return Success(InstallerFlow(args[1], args[2], payloadRoot: null, product));

        if (args.Length == 4 && args[0] == "validate-installer-flow")
            return Success(InstallerFlow(args[1], args[2], Path.GetFullPath(args[3]), product));

        if (args.Length == 5 && args[0] is "validate-package" or "validate-installed-service")
        {
            return Success(new SmokeCommandRequest(
                args[0],
                args[1],
                args[2],
                Path.GetFullPath(args[3]),
                Path.GetFullPath(args[4]),
                PayloadRoot: null,
                product));
        }

        return new SmokeCommandParseResult(null, Usage);
    }

    private static SmokeCommandParseResult Success(SmokeCommandRequest request)
        => new(request, Usage);

    private static SmokeCommandRequest InstallerFlow(
        string platform,
        string workDirectory,
        string? payloadRoot,
        SmokeProductConfig? product)
        => new(
            "validate-installer-flow",
            platform,
            RuntimeId: "",
            ArtifactDirectory: "",
            Path.GetFullPath(workDirectory),
            payloadRoot,
            product);

    private static (string[]? Args, SmokeProductConfig? ProductConfig) ParseOptions(string[] args)
    {
        var remaining = new List<string>();
        SmokeProductConfig? product = null;

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--product-manifest")
            {
                if (i + 1 >= args.Length || product is not null)
                    return (null, null);

                product = LoadProductManifest(Path.GetFullPath(args[++i]));
            }
            else
            {
                remaining.Add(args[i]);
            }
        }

        return (remaining.ToArray(), product);
    }

    private static SmokeProductConfig LoadProductManifest(string path)
    {
        var manifest = JsonSerializer.Deserialize<SmokeProductConfig>(
            File.ReadAllText(path),
            Options);

        return manifest ?? throw new InvalidOperationException("Smoke product manifest is empty.");
    }
}
