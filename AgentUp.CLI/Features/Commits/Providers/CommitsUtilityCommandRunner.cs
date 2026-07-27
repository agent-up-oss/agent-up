using AgentUp.CLI.Features.Commits.DTOs;
using AgentUp.CLI.Features.Commits.Interfaces;
using AgentUp.CLI.Features.Commits.Services;

namespace AgentUp.CLI.Features.Commits.Providers;

public sealed class CommitsUtilityCommandRunner(
    CommitsService service,
    CommitsOutputService output,
    ICommitsFormatParser formatParser) : ICommitsUtilityCommandRunner
{
    public async Task<int> RunInspectAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var includePatch = args.Contains("--patch", StringComparer.Ordinal);
        var entryRef = Positionals(args).FirstOrDefault();
        var (format, error) = formatParser.Parse(OnlyFormatArgs(args));
        if (error is not null)
            return output.WriteError(error, format);

        if (string.IsNullOrWhiteSpace(entryRef))
            return output.WriteError("Missing required argument: <entry>", format);

        try
        {
            var result = await service.InspectAsync(entryRef, includePatch, cancellationToken);
            return output.WriteInspect(result, format);
        }
        catch (InvalidOperationException ex)
        {
            return output.WriteError(ex.Message, format);
        }
    }

    public async Task<int> RunEditAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var (format, error) = formatParser.Parse(OnlyFormatArgs(args));
        if (error is not null)
            return output.WriteError(error, format);

        var positionals = Positionals(args);
        var verb = positionals.FirstOrDefault() ?? "";
        try
        {
            return verb switch
            {
                "begin" => await BeginAsync(args, format, cancellationToken),
                "save" => output.WriteEdit(await service.SaveEditAsync(cancellationToken), format),
                "abort" => output.WriteEdit(await service.AbortEditAsync(cancellationToken), format),
                "status" => output.WriteStatus(await service.GetStatusAsync(cancellationToken), format),
                _ => output.WriteError("Usage: agentup commits edit <begin|save|abort|status>", format)
            };
        }
        catch (InvalidOperationException ex)
        {
            return output.WriteError(ex.Message, format);
        }
    }

    public async Task<int> RunEntryAsync(string command, string[] args, CancellationToken cancellationToken = default)
    {
        var (format, error) = formatParser.Parse(OnlyFormatArgs(args));
        if (error is not null)
            return output.WriteError(error, format);

        try
        {
            return command switch
            {
                "message" => output.WriteEdit(await service.UpdateMessageAsync(EntryRef(args), RequiredValue(args, "--message"), cancellationToken), format),
                "tests" => output.WriteEdit(await service.SetTestsAsync(EntryRef(args), ValuesAfter(args, "--set"), cancellationToken), format),
                "files" => await FilesAsync(args, format, cancellationToken),
                "remove" => output.WriteEdit(await service.RemoveAsync(EntryRef(args), cancellationToken), format),
                "restore" => output.WriteEdit(await service.RestoreArchivedAsync(EntryRef(args), cancellationToken), format),
                _ => output.WriteError("Unknown commits entry command.", format)
            };
        }
        catch (InvalidOperationException ex)
        {
            return output.WriteError(ex.Message, format);
        }
    }

    private async Task<int> BeginAsync(string[] args, CommitsOutputFormat format, CancellationToken cancellationToken)
    {
        var entryRef = Positionals(args).Skip(1).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(entryRef))
            return output.WriteError("Missing required argument: <entry>", format);

        var result = await service.BeginEditAsync(entryRef, cancellationToken);
        return output.WriteEdit(result, format);
    }

    private async Task<int> FilesAsync(string[] args, CommitsOutputFormat format, CancellationToken cancellationToken)
    {
        if (args.Contains("--add", StringComparer.Ordinal))
            return output.WriteEdit(await service.AddFilesAsync(EntryRef(args), ValuesAfter(args, "--add"), cancellationToken), format);
        if (args.Contains("--remove", StringComparer.Ordinal))
            return output.WriteEdit(await service.RemoveFilesAsync(EntryRef(args), ValuesAfter(args, "--remove"), cancellationToken), format);

        return output.WriteError("Missing required argument: --add or --remove", format);
    }

    private static string EntryRef(string[] args)
        => Positionals(args).FirstOrDefault() ?? throw new InvalidOperationException("Missing required argument: <entry>");

    private static string RequiredValue(string[] args, string flag)
    {
        var index = Array.IndexOf(args, flag);
        if (index < 0 || index + 1 >= args.Length)
            throw new InvalidOperationException($"Missing required argument: {flag}");
        return args[index + 1];
    }

    private static IReadOnlyList<string> ValuesAfter(string[] args, string flag)
    {
        var index = Array.IndexOf(args, flag);
        if (index < 0)
            throw new InvalidOperationException($"Missing required argument: {flag}");

        var values = args.Skip(index + 1).TakeWhile(a => !a.StartsWith("--", StringComparison.Ordinal)).ToList();
        if (values.Count == 0)
            throw new InvalidOperationException($"Missing required value for {flag}");
        return values;
    }

    private static IReadOnlyList<string> Positionals(string[] args)
    {
        var result = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--format")
            {
                i++;
                continue;
            }

            if (args[i] is "--message" or "--set" or "--add" or "--remove")
            {
                i++;
                while (i < args.Length && !args[i].StartsWith("--", StringComparison.Ordinal))
                    i++;
                i--;
                continue;
            }
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
                result.Add(args[i]);
        }
        return result;
    }

    private static string[] OnlyFormatArgs(string[] args)
    {
        var result = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] != "--format")
                continue;
            result.Add(args[i]);
            if (i + 1 < args.Length)
                result.Add(args[++i]);
        }
        return [.. result];
    }
}
