using AgentUp.Server.Features.Workspaces.Interfaces;

namespace AgentUp.Server.Features.Workspaces.Providers;

public sealed class WorkspaceDiskUsageProvider : IWorkspaceDiskUsageProvider
{
    public long Measure(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return 0;

        return MeasureDirectory(Path.GetFullPath(path));
    }

    private static long MeasureDirectory(string path)
    {
        var total = SumFileLengths(path);
        foreach (var directory in EnumerateDirectories(path))
            total += MeasureDirectory(directory);

        return total;
    }

    private static long SumFileLengths(string path)
    {
        long total = 0;
        foreach (var file in EnumerateFiles(path))
        {
            try
            {
                total += new FileInfo(file).Length;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                continue;
            }
        }

        return total;
    }

    private static IEnumerable<string> EnumerateFiles(string path)
    {
        try
        {
            return Directory.GetFiles(path);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
        {
            return [];
        }
    }

    private static IEnumerable<string> EnumerateDirectories(string path)
    {
        try
        {
            return Directory.GetDirectories(path);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
        {
            return [];
        }
    }
}
