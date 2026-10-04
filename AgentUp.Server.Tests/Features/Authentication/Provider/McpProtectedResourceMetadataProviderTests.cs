using AgentUp.Server.Features.Authentication.DTOs;
using AgentUp.Server.Features.Authentication.Providers;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Authentication.Provider;

[TestFixture]
public sealed class McpProtectedResourceMetadataProviderTests
{
    private const string Issuer = "https://issuer.test/";

    [Test]
    public void MetadataIsUnconfiguredWithoutAnIssuer()
    {
        Assert.That(Provider(issuer: null).IsConfigured, Is.False);
    }

    [TestCase("issuer.test")]
    [TestCase("/relative")]
    [TestCase("file:///issuer")]
    public void AnIssuerThatIsNotAnAbsoluteHttpUriDoesNotConfigureMetadata(string issuer)
    {
        Assert.That(Provider(issuer).IsConfigured, Is.False);
    }

    [Test]
    public void MetadataNamesTheConfiguredIssuerAsTheAuthorizationServer()
    {
        var metadata = Provider(Issuer).Create();

        Assert.Multiple(() =>
        {
            Assert.That(metadata.AuthorizationServers, Is.EqualTo(new[] { Issuer }));
            Assert.That(metadata.BearerMethodsSupported, Is.EqualTo(new[] { "header" }));
        });
    }

    [Test]
    public void MetadataAdvertisesEveryOperationPermissionAsAScope()
    {
        var metadata = Provider(Issuer).Create();

        Assert.That(metadata.ScopesSupported, Is.EqualTo(OperationPermissions.All));
    }

    [Test]
    public void MetadataLeavesTheResourceIdentifierForTheRequestToDetermine()
    {
        var metadata = Provider(Issuer).Create();

        Assert.That(metadata.Resource, Is.Null);
    }

    [Test]
    public void CreatingMetadataWithoutAnIssuerIsRejected()
    {
        var provider = Provider(issuer: null);

        Assert.That(provider.Create, Throws.InstanceOf<InvalidOperationException>());
    }

    private static McpProtectedResourceMetadataProvider Provider(string? issuer)
        => new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AGENTUP_EXTERNAL_ISSUER"] = issuer })
            .Build());
}
