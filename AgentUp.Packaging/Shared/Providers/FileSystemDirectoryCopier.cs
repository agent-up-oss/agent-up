namespace AgentUp.Packaging.Shared.Providers;

public static class FileSystemDirectoryCopier
{
    public static void Copy(string source, string destination)
    {
        var fullSource = PackagePathValidator.RequireFullyQualifiedPath(source, nameof(source));
        var fullDestination = PackagePathValidator.RequireFullyQualifiedPath(destination, nameof(destination));
        CopyDirectoryRecursive(new DirectoryInfo(fullSource), new DirectoryInfo(fullDestination));
    }

    private static void CopyDirectoryRecursive(DirectoryInfo source, DirectoryInfo destination)
    {
        Directory.CreateDirectory(destination.FullName);
        foreach (var file in source.GetFiles())
            file.CopyTo(PackagePathValidator.RequireChildPath(destination.FullName, file.Name, nameof(file)), overwrite: true);

        foreach (var child in source.GetDirectories())
            CopyDirectoryRecursive(child, new DirectoryInfo(PackagePathValidator.RequireChildPath(destination.FullName, child.Name, nameof(child))));
    }
}
