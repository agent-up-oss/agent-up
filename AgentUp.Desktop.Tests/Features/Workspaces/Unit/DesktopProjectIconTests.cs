using System.Xml.Linq;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Unit;

[TestFixture]
public class DesktopProjectIconTests
{
    [Test]
    public void DesktopProject_declaresWindowsExecutableIcon()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectPath = Path.Join(repositoryRoot, "AgentUp.Desktop", "AgentUp.Desktop.csproj");
        var document = XDocument.Load(projectPath);

        var iconPath = document
            .Descendants("ApplicationIcon")
            .Select(element => element.Value)
            .SingleOrDefault();

        Assert.That(iconPath, Is.EqualTo(@"..\media\logo.ico"));
        var normalizedIconPath = iconPath!.Replace('\\', Path.DirectorySeparatorChar);
        Assert.That(File.Exists(Path.GetFullPath(Path.Join(Path.GetDirectoryName(projectPath)!, normalizedIconPath))), Is.True);
    }

    private static string FindRepositoryRoot()
    {
        var directory = TestContext.CurrentContext.TestDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Join(directory, "agent-up.sln")))
                return directory;

            var parent = Directory.GetParent(directory)?.FullName;
            if (parent == directory)
                break;

            directory = parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
