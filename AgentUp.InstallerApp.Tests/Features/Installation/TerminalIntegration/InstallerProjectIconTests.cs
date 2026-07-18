using System.Xml.Linq;
using AgentUp.InstallerApp.Features.Installation.Views;

namespace AgentUp.InstallerApp.Tests.Features.Installation.TerminalIntegration;

[TestFixture]
public class InstallerProjectIconTests
{
    [Test]
    public void InstallerProject_declaresExecutableAndWindowIconAssets()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectPath = Path.Join(repositoryRoot, "AgentUp.InstallerApp", "AgentUp.InstallerApp.csproj");
        var document = XDocument.Load(projectPath);

        var applicationIcon = document
            .Descendants("ApplicationIcon")
            .Select(element => element.Value)
            .SingleOrDefault();

        Assert.That(applicationIcon, Is.EqualTo(@"..\media\logo.ico"));
        var normalizedIconPath = applicationIcon!.Replace('\\', Path.DirectorySeparatorChar);
        Assert.That(File.Exists(Path.GetFullPath(Path.Join(Path.GetDirectoryName(projectPath)!, normalizedIconPath))), Is.True);

        Assert.That(document.ToString(), Does.Contain("media/logo.png"));
        Assert.That(InstallerWindow.FindWindowIconPath(), Does.EndWith(Path.Join("media", "logo.png")));
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
