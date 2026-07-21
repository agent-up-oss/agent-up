using AgentUp.Server.Features.Mcp.DTOs;
using AgentUp.Server.Features.Mcp.Interfaces;

namespace AgentUp.Server.Features.Mcp.Providers;

public sealed class GitWorkspaceIdentityProvider : IWorkspaceIdentityProvider
{
    public async Task<WorkspaceIdentity> ReadAsync(string worktreePath, CancellationToken cancellationToken)
    {
        try
        {
            var safeWorktreePath = ValidateAndNormalizeWorktreePath(worktreePath);
            var repository = await FindRepositoryAsync(safeWorktreePath, cancellationToken);
            var head = await ReadHeadAsync(repository, cancellationToken);

            return new WorkspaceIdentity(
                RepositoryPath: repository.RepositoryPath,
                Branch: head.Branch,
                Commit: head.Commit);
        }
        catch (InvalidOperationException)
        {
            return CreateFallbackIdentity(worktreePath);
        }
        catch (IOException)
        {
            return CreateFallbackIdentity(worktreePath);
        }
        catch (UnauthorizedAccessException)
        {
            return CreateFallbackIdentity(worktreePath);
        }
    }

    private static WorkspaceIdentity CreateFallbackIdentity(string worktreePath) =>
        new(
            RepositoryPath: worktreePath,
            Branch: "not on a git branch",
            Commit: "");

    private static string ValidateAndNormalizeWorktreePath(string worktreePath)
    {
        if (string.IsNullOrWhiteSpace(worktreePath))
            throw new InvalidOperationException("worktreePath is required.");

        var fullPath = Path.GetFullPath(worktreePath);
        if (!Directory.Exists(fullPath))
            throw new InvalidOperationException($"worktreePath does not exist: {fullPath}");

        return fullPath;
    }

    private static async Task<GitRepository> FindRepositoryAsync(
        string startPath,
        CancellationToken cancellationToken)
    {
        var directory = new DirectoryInfo(startPath);
        while (directory is not null)
        {
            var gitPath = Path.Join(directory.FullName, ".git");
            if (Directory.Exists(gitPath))
            {
                var commonDir = await ReadCommonDirectoryAsync(gitPath, cancellationToken);
                return new GitRepository(directory.FullName, gitPath, commonDir, directory.FullName);
            }

            if (File.Exists(gitPath))
            {
                var gitDirectory = await ReadLinkedGitDirectoryAsync(gitPath, cancellationToken);
                var commonDir = await ReadCommonDirectoryAsync(gitDirectory, cancellationToken);
                return new GitRepository(directory.FullName, gitDirectory, commonDir, GetLinkedRepositoryPath(directory.FullName, commonDir));
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Git metadata was not found.");
    }

    private static async Task<string> ReadLinkedGitDirectoryAsync(
        string gitFilePath,
        CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(gitFilePath, cancellationToken);
        var prefix = "gitdir:";
        if (!content.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(".git file does not contain a gitdir entry.");

        var gitDirectory = content[prefix.Length..].Trim();
        return Path.GetFullPath(gitDirectory, Path.GetDirectoryName(gitFilePath)!);
    }

    private static async Task<string> ReadCommonDirectoryAsync(
        string gitDirectory,
        CancellationToken cancellationToken)
    {
        var commonDirPath = Path.Join(gitDirectory, "commondir");
        if (!File.Exists(commonDirPath))
            return gitDirectory;

        var commonDir = (await File.ReadAllTextAsync(commonDirPath, cancellationToken)).Trim();
        return Path.GetFullPath(commonDir, gitDirectory);
    }

    private static string GetLinkedRepositoryPath(string worktreeRoot, string commonDir)
    {
        var commonDirectory = new DirectoryInfo(commonDir);
        return string.Equals(commonDirectory.Name, ".git", StringComparison.Ordinal)
            ? commonDirectory.Parent?.FullName ?? worktreeRoot
            : worktreeRoot;
    }

    private static async Task<GitHead> ReadHeadAsync(
        GitRepository repository,
        CancellationToken cancellationToken)
    {
        var headPath = Path.Join(repository.GitDirectory, "HEAD");
        var head = (await File.ReadAllTextAsync(headPath, cancellationToken)).Trim();

        if (!head.StartsWith("ref:", StringComparison.Ordinal))
            return new GitHead("HEAD", head);

        var referenceName = head["ref:".Length..].Trim();
        var commit = await ReadReferenceAsync(repository, referenceName, cancellationToken);
        var branch = referenceName.StartsWith("refs/heads/", StringComparison.Ordinal)
            ? referenceName["refs/heads/".Length..]
            : referenceName;

        return new GitHead(branch, commit);
    }

    private static async Task<string> ReadReferenceAsync(
        GitRepository repository,
        string referenceName,
        CancellationToken cancellationToken)
    {
        var referencePath = Path.Join(repository.GitDirectory, referenceName);
        if (!File.Exists(referencePath))
            referencePath = Path.Join(repository.CommonDirectory, referenceName);

        if (File.Exists(referencePath))
            return (await File.ReadAllTextAsync(referencePath, cancellationToken)).Trim();

        var packedReference = await ReadPackedReferenceAsync(repository.CommonDirectory, referenceName, cancellationToken);
        if (!string.IsNullOrWhiteSpace(packedReference))
            return packedReference;

        throw new InvalidOperationException($"Git reference was not found: {referenceName}");
    }

    private static async Task<string?> ReadPackedReferenceAsync(
        string commonDirectory,
        string referenceName,
        CancellationToken cancellationToken)
    {
        var packedRefsPath = Path.Join(commonDirectory, "packed-refs");
        if (!File.Exists(packedRefsPath))
            return null;

        await foreach (var line in File.ReadLinesAsync(packedRefsPath, cancellationToken))
        {
            var candidateReference = line.Length > 0 && line[0] is not ('#' or '^');
            if (candidateReference)
            {
                var separatorIndex = line.IndexOf(' ', StringComparison.Ordinal);
                if (separatorIndex <= 0)
                    continue;

                if (string.Equals(line[(separatorIndex + 1)..], referenceName, StringComparison.Ordinal))
                    return line[..separatorIndex];
            }
        }

        return null;
    }

    private sealed record GitRepository(
        string WorktreeRoot,
        string GitDirectory,
        string CommonDirectory,
        string RepositoryPath);

    private sealed record GitHead(string Branch, string Commit);
}
