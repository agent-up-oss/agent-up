using PuppeteerSharp;

namespace AgentUp.Server.Features.Browser.Models;

public sealed record BrowserSessionState(string WorkspaceId, IBrowser Browser, IPage Page);
