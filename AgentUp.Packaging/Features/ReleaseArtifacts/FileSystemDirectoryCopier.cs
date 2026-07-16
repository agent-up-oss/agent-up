namespace AgentUp.Packaging.Features.ReleaseArtifacts;

public static class FileSystemDirectoryCopier
{
    public static void Copy(string source, string destination)
        => CopyDirectoryRecursive(new DirectoryInfo(source), new DirectoryInfo(destination));

    private static void CopyDirectoryRecursive(DirectoryInfo source, DirectoryInfo destination)
    {
        Directory.CreateDirectory(destination.FullName);
        foreach (var file in source.GetFiles())
            file.CopyTo(Path.Join(destination.FullName, file.Name), overwrite: true);

        foreach (var child in source.GetDirectories())
            CopyDirectoryRecursive(child, new DirectoryInfo(Path.Join(destination.FullName, child.Name)));
    }
}
