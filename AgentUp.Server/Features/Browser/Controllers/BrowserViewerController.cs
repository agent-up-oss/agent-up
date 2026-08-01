using AgentUp.Server.Features.Browser.Resources;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Browser.Controllers;

[Route("api/browser")]
public sealed class BrowserViewerController : ControllerBase
{
    [HttpGet("viewer")]
    public ContentResult Viewer([FromQuery] string workspaceId)
        => Content(ScreencastViewerPage.Build(workspaceId), "text/html");

    [HttpGet("mode")]
    public ContentResult Mode([FromServices] IConfiguration config)
        => Content(
            string.Equals(config["Browser:Mode"], "headless", StringComparison.OrdinalIgnoreCase)
                ? "headless"
                : "polling",
            "text/plain");
}
