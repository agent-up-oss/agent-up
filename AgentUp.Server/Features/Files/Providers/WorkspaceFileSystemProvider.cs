using AgentUp.Server.Features.Files.DTOs;
using AgentUp.Server.Features.Files.Interfaces;
using AgentUp.Server.Features.Files.Models;

namespace AgentUp.Server.Features.Files.Providers;

/// <summary>
/// Reads and writes inside one workspace folder. A request path is a client's, so it is treated
/// as data: it must be relative, use <c>/</c>, never step out with <c>..</c>, never name the
/// repository's <c>.git</c>, and still resolve inside the folder once every link on the way has
/// been followed. A link that leads out of the folder is left out of listings rather than shown
/// as something that cannot be opened.
/// </summary>
public sealed class WorkspaceFileSystemProvider : IWorkspaceFileSystemProvider
{
    private const string GitDirectory = ".git";

    public WorkspaceDiskListing? ListDirectory(string root, string path, int maxEntries)
    {
        var rootReal = RootOf(root);
        var relative = Normalize(path);
        var full = Resolve(rootReal, relative);
        if (!Directory.Exists(full))
            return null;

        var ordered = new DirectoryInfo(full).EnumerateFileSystemInfos()
            .Where(info => !IsGitDirectory(info.Name))
            .Select(info => Describe(rootReal, relative, info))
            .OfType<WorkspaceDiskEntry>()
            .OrderBy(entry => entry.Kind == WorkspaceFileEntryKind.Directory ? 0 : 1)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Name, StringComparer.Ordinal)
            .ToList();
        var truncated = ordered.Count > maxEntries;
        return new WorkspaceDiskListing(relative, truncated ? ordered.Take(maxEntries).ToList() : ordered, truncated);
    }

    public async Task<WorkspaceDiskFile?> ReadFileAsync(string root, string path, long maxBytes, CancellationToken cancellationToken)
    {
        var rootReal = RootOf(root);
        var relative = RequireFilePath(path);
        var full = Resolve(rootReal, relative);
        if (!File.Exists(full))
            return null;

        var size = new FileInfo(full).Length;
        if (size > maxBytes)
            return new WorkspaceDiskFile(relative, size, null);
        var bytes = await File.ReadAllBytesAsync(full, cancellationToken);
        return new WorkspaceDiskFile(relative, bytes.LongLength, bytes);
    }

    public async Task WriteFileAsync(string root, string path, byte[] bytes, CancellationToken cancellationToken)
    {
        var rootReal = RootOf(root);
        var relative = RequireFilePath(path);
        var full = Resolve(rootReal, relative);
        if (!File.Exists(full))
            throw new FileNotFoundException($"'{relative}' is not a file in this workspace.");

        // Truncate rather than create: an edit replaces what is there and never makes a new file,
        // and writing in place keeps the file's mode and ownership as they were.
        await using var stream = new FileStream(full, FileMode.Truncate, FileAccess.Write, FileShare.Read);
        await stream.WriteAsync(bytes, cancellationToken);
    }

    private static WorkspaceDiskEntry? Describe(string rootReal, string parent, FileSystemInfo info)
    {
        var childPath = parent.Length == 0 ? info.Name : $"{parent}/{info.Name}";
        var target = info.LinkTarget is null ? info : LinkTargetInside(rootReal, info);
        if (target is null)
            return null;
        return target is DirectoryInfo
            ? new WorkspaceDiskEntry(info.Name, childPath, WorkspaceFileEntryKind.Directory, null)
            : new WorkspaceDiskEntry(info.Name, childPath, WorkspaceFileEntryKind.File, ((FileInfo)target).Length);
    }

    /// <summary>
    /// Where a link finally leads, or null when it is broken, loops, or leaves the folder. One
    /// such link is left out rather than failing the listing of the folder around it.
    /// </summary>
    private static FileSystemInfo? LinkTargetInside(string rootReal, FileSystemInfo link)
    {
        try
        {
            var resolved = link.ResolveLinkTarget(returnFinalTarget: true);
            return resolved is { Exists: true } && IsUnder(rootReal, Path.GetFullPath(resolved.FullName)) ? resolved : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static string RootOf(string root)
    {
        var full = Path.GetFullPath(root);
        return RealPath(full) ?? full;
    }

    private static string RequireFilePath(string path)
    {
        var relative = Normalize(path);
        if (relative.Length == 0)
            throw new WorkspaceFilePathException("Name a file inside the workspace.");
        return relative;
    }

    /// <summary>The request path as segments joined by <c>/</c>, with <c>.</c> segments dropped.</summary>
    private static string Normalize(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;
        if (path.Contains('\0') || path.Contains('\\') || path.Contains('\r') || path.Contains('\n'))
            throw new WorkspaceFilePathException($"'{path}' is not a workspace path.");
        if (path.StartsWith('/') || Path.IsPathRooted(path))
            throw new WorkspaceFilePathException($"'{path}' must be relative to the workspace folder.");

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries).Where(segment => segment != ".").ToList();
        if (segments.Contains(".."))
            throw new WorkspaceFilePathException($"'{path}' must stay inside the workspace folder.");
        if (segments.Any(IsGitDirectory))
            throw new WorkspaceFilePathException("The repository's .git folder is not part of the workspace files.");
        return string.Join('/', segments);
    }

    /// <summary>The real location a relative path names, refused when a link takes it outside the folder.</summary>
    private static string Resolve(string rootReal, string relative)
    {
        // Normalize has already refused "..", rooted paths and .git, so the joined path is under
        // the folder; only a link along the way can still lead out, which the real path shows.
        var joined = relative.Length == 0 ? rootReal : Path.GetFullPath(Path.Join(rootReal, relative));
        var real = RealPath(joined) ?? joined;
        if (!IsUnder(rootReal, real))
            throw new WorkspaceFilePathException($"'{relative}' leads outside the workspace folder.");
        return real;
    }

    /// <summary>
    /// The path with every link along it followed, or null when part of it does not exist. A
    /// missing path is answered by the caller as not found, so null is not an error here.
    /// </summary>
    private static string? RealPath(string fullPath)
    {
        var root = Path.GetPathRoot(fullPath) ?? "/";
        var current = root;
        foreach (var segment in fullPath[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            var next = Path.Join(current, segment);
            FileSystemInfo info = Directory.Exists(next) ? new DirectoryInfo(next) : new FileInfo(next);
            if (info.LinkTarget is null)
            {
                if (!info.Exists)
                    return null;
                current = next;
                continue;
            }

            // A link loop throws IOException here, which the caller answers as an unreadable path.
            var target = info.ResolveLinkTarget(returnFinalTarget: true);
            if (target is not { Exists: true })
                return null;
            current = Path.GetFullPath(target.FullName);
        }
        return current;
    }

    private static bool IsUnder(string rootReal, string candidate)
    {
        if (string.Equals(rootReal, candidate, StringComparison.Ordinal))
            return true;
        var relative = Path.GetRelativePath(rootReal, candidate);
        return relative != ".."
               && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
               && !Path.IsPathRooted(relative);
    }

    private static bool IsGitDirectory(string name) => string.Equals(name, GitDirectory, StringComparison.OrdinalIgnoreCase);
}
