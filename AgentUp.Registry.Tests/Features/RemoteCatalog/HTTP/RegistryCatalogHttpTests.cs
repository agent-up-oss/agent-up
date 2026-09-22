using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AgentUp.Registry.Tests.Support;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Registry.Tests.Features.RemoteCatalog.HTTP;

[TestFixture]
public sealed class RegistryCatalogHttpTests
{
    [Test]
    public async Task List_returns_an_empty_catalog_from_an_empty_registry()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/packages");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var json = await response.Content.ReadAsStringAsync();
        Assert.That(json, Does.Contain("packages"));
    }

    [Test]
    public async Task Push_without_a_token_is_unauthorized()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var content = new ByteArrayContent([1, 2, 3]);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");

        using var response = await client.PutAsync(
            $"/packages/{RegistryDomain.DotnetId}/{RegistryDomain.DotnetVersion}",
            content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    /// <summary>
    /// A push must land under the registry the configuration names.
    /// </summary>
    /// <remarks>
    /// A test host applies its configuration while the host is being built, so a registry root
    /// read any earlier falls back to the content root - which, when the Registry runs from its
    /// own project, is the working copy. That is how packages ended up committed under
    /// <c>AgentUp.Registry/</c>.
    /// </remarks>
    [Test]
    public async Task Authorized_push_stores_the_package_under_the_configured_registry()
    {
        var registry = CreateRegistryRoot();
        using var factory = CreateFactory(registry);
        using var client = factory.CreateClient();
        using var content = new ByteArrayContent(ZipDotnetPackage());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "secret-token");

        using var push = await client.PutAsync(
            $"/packages/{RegistryDomain.DotnetId}/{RegistryDomain.DotnetVersion}",
            content);

        Assert.That(push.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.Multiple(() =>
        {
            Assert.That(
                File.Exists(Path.Join(registry, "packages", RegistryDomain.DotnetId, RegistryDomain.DotnetVersion, "capability.json")),
                Is.True,
                "the pushed package belongs to the configured registry root");
            Assert.That(
                Directory.Exists(Path.Join(registry, "staging")),
                Is.True,
                "the archive is staged inside the registry root, not beside the running project");
        });
    }

    [Test]
    public async Task Authorized_push_then_list_and_download_round_trips_the_package()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var content = new ByteArrayContent(ZipDotnetPackage());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "secret-token");

        using var push = await client.PutAsync(
            $"/packages/{RegistryDomain.DotnetId}/{RegistryDomain.DotnetVersion}",
            content);
        Assert.That(push.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        using var list = await client.GetAsync("/packages");
        var listed = await list.Content.ReadAsStringAsync();
        Assert.That(listed, Does.Contain(RegistryDomain.DotnetId));

        using var download = await client.GetAsync($"/packages/{RegistryDomain.DotnetId}/{RegistryDomain.DotnetVersion}");
        Assert.That(download.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await download.Content.ReadAsByteArrayAsync(), Is.Not.Empty);
    }

    private static byte[] ZipDotnetPackage()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("capability.json");
            using var writer = new StreamWriter(entry.Open());
            writer.Write(JsonSerializer.Serialize(RegistryDomain.DotnetPackage(), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        }

        return stream.ToArray();
    }

    private static string CreateRegistryRoot()
        => Directory.CreateDirectory(
            Path.Join(Path.GetTempPath(), "agent-up-http-registry-" + Guid.NewGuid().ToString("N"))).FullName;

    private static WebApplicationFactory<AgentUp.Registry.Program> CreateFactory(string? registryRoot = null)
        => new WebApplicationFactory<AgentUp.Registry.Program>().WithWebHostBuilder(builder =>
        {
            var registry = registryRoot ?? CreateRegistryRoot();
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AGENTUP_CAPABILITY_REGISTRY_PATH"] = registry,
                    ["AGENTUP_REGISTRY_TOKEN"] = "secret-token"
                });
            });
        });
}
