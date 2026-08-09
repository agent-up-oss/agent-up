using System.Xml.Linq;
using AgentUp.InstallerApp.Features.Installation.Views;

namespace AgentUp.InstallerApp.Tests.Features.Installation.Provider;

[TestFixture]
public class InstallerProjectIconTests
{
    [Test]
    public void InstallerProject_declaresWindowIconPackageAsset()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectPath = Path.Join(repositoryRoot, "LocalInstaller.App", "LocalInstaller.App.csproj");
        var document = XDocument.Load(projectPath);

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
