using AgentUp.Registry.Features.RemoteCatalog.DTOs;
using AgentUp.Registry.Shared.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Registry.Features.RemoteCatalog.Controllers;

[ApiController]
[Route("packages")]
public sealed class RegistryCatalogHttpController(
    RemoteCatalogController catalog,
    IRegistryPathValidator paths,
    IConfiguration configuration)
    : ControllerBase
{
    [HttpGet]
    public ActionResult<RemoteCatalogListDto> List() => Ok(catalog.ListLocal());

    [HttpGet("{id}/{version}")]
    public IActionResult Download(string id, string version)
        => File(catalog.Export(id, version).Archive, "application/zip");

    [HttpPut("{id}/{version}")]
    public async Task<IActionResult> Push(string id, string version, CancellationToken cancellationToken)
    {
        if (!IsAuthorized())
            return Unauthorized();

        catalog.Import(new RemotePackageBytesDto(id, version, await ReadBodyAsync(cancellationToken)), StagingRoot());
        return NoContent();
    }

    private bool IsAuthorized()
    {
        var expected = configuration["AGENTUP_REGISTRY_TOKEN"] ?? "";
        return expected.Length > 0
               && Request.Headers.Authorization.ToString().Equals("Bearer " + expected, StringComparison.Ordinal);
    }

    private async Task<byte[]> ReadBodyAsync(CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        await Request.Body.CopyToAsync(stream, cancellationToken);
        return stream.ToArray();
    }

    // The registry root, not the content root: staging beside the running project writes a pushed
    // package into the working copy when the Registry runs from its own project directory.
    private string StagingRoot() => Path.Join(paths.RegistryRoot, "staging");
}
