namespace AgentUp.Packaging.Features.ReleaseArtifacts;

public abstract class SymbolicLinkPackageFileSystem : UnixPackageFileSystem, ISymbolicLinkPackageFileSystem
{
    public void CreateSymbolicLink(string linkPath, string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(linkPath)!);
        if (File.Exists(linkPath) || Directory.Exists(linkPath))
            File.Delete(linkPath);

        File.CreateSymbolicLink(linkPath, targetPath);
    }
}
