using System.Security.Cryptography;
using System.Text;

namespace AgentUp.Desktop.Features.FakeServer.Providers;

public sealed class FakeApplicationPageProvider
{
    public Uri Write(string workspaceId, string tabKey, string html)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(workspaceId + ":" + tabKey)));
        var htmlPath = Path.Join(Path.GetTempPath(), $"agentup-fake-app-{hash[..16]}.html");
        File.WriteAllText(htmlPath, html, Encoding.UTF8);
        return new Uri("file://" + htmlPath);
    }
}
