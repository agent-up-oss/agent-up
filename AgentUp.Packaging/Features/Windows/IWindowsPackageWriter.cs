namespace AgentUp.Packaging.Features.Windows;

public interface IWindowsPackageWriter
{
    void ResetDirectory(string path);
    void CreateDirectory(string path);
    void WriteText(string path, string text);
    void CopyFile(string sourcePath, string destinationPath);
}
