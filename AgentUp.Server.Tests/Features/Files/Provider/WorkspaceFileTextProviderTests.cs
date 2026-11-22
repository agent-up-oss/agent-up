using System.Text;
using AgentUp.Server.Features.Files.Providers;

namespace AgentUp.Server.Tests.Features.Files.Provider;

[TestFixture]
public sealed class WorkspaceFileTextProviderTests
{
    [Test]
    public void Decode_readsUtf8AndKeepsLineEndings()
    {
        var text = new WorkspaceFileTextProvider().Decode(Encoding.UTF8.GetBytes("café\r\nnext\n"));

        Assert.That(text!.Text, Is.EqualTo("café\r\nnext\n"));
        Assert.That(text.HasBom, Is.False);
    }

    [Test]
    public void Decode_treatsANulByteAsBinary()
        => Assert.That(new WorkspaceFileTextProvider().Decode([(byte)'a', 0, (byte)'b']), Is.Null);

    [Test]
    public void Decode_treatsInvalidUtf8AsBinary()
        => Assert.That(new WorkspaceFileTextProvider().Decode([0xC3, 0x28]), Is.Null);

    [Test]
    public void EncodeRoundTripsAByteOrderMark()
    {
        var provider = new WorkspaceFileTextProvider();
        byte[] original = [0xEF, 0xBB, 0xBF, (byte)'x'];

        var text = provider.Decode(original)!;

        Assert.That(text.HasBom, Is.True);
        Assert.That(provider.Encode(text.Text, text.HasBom), Is.EqualTo(original));
    }

    [Test]
    public void Version_namesTheExactBytes()
    {
        var provider = new WorkspaceFileTextProvider();

        Assert.That(provider.Version("a"u8.ToArray()), Is.EqualTo(provider.Version("a"u8.ToArray())));
        Assert.That(provider.Version("a"u8.ToArray()), Is.Not.EqualTo(provider.Version("a\n"u8.ToArray())));
    }
}
