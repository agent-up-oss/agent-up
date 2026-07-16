using AgentUp.Packaging.Features.ReleaseArtifacts.Interfaces;
namespace AgentUp.Packaging.Features.ReleaseArtifacts.Providers;

public abstract class PackageFileSystem : IPackageFileSystem
{
    public void ResetDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);

        Directory.CreateDirectory(path);
    }

    public void CreateDirectory(string path)
        => Directory.CreateDirectory(path);

    public void CopyFile(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
    }

    public void WriteText(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
