using System.Diagnostics;
using System.Text.Json;
using AgentUp.Server.Features.Browser.Models;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;

namespace AgentUp.Server.Features.Browser.Services;

public sealed class CdpBrowserExecutor(ILogger<CdpBrowserExecutor> logger)
{
    public async Task<BrowserCommandResultDto> ExecuteAsync(
        BrowserSessionState session,
        BrowserCommandDto command,
        CancellationToken ct)
    {
        try
        {
            return command.Kind switch
            {
                BrowserCommandKind.Navigate => await AttachPageStateAsync(await NavigateAsync(session, command, ct), session, command),
                BrowserCommandKind.InspectPage => await InspectAsync(session, command),
                BrowserCommandKind.Click => await AttachPageStateAsync(await EvalAsync(session, command, BrowserScriptProvider.Click(command.Selector!)), session, command),
                BrowserCommandKind.Fill => await AttachPageStateAsync(await EvalAsync(session, command, BrowserScriptProvider.Fill(command.Selector!, command.Text!)), session, command),
                BrowserCommandKind.Press => await AttachPageStateAsync(await PressAsync(session, command), session, command),
                BrowserCommandKind.WaitForSelector => await AttachPageStateAsync(await WaitForSelectorAsync(session, command, ct), session, command),
                BrowserCommandKind.WaitForText => await AttachPageStateAsync(await WaitForConditionAsync(session, command, BrowserScriptProvider.CheckText(command.Text!), ct), session, command),
                BrowserCommandKind.WaitForNavigation => await AttachPageStateAsync(await WaitForNavigationAsync(session, command, ct), session, command),
                BrowserCommandKind.Screenshot => await ScreenshotAsync(session, command, ct),
                _ => Fail(command, $"Unknown command kind: {command.Kind}")
            };
        }
        catch (Exception ex) when (ex is PuppeteerException or TimeoutException or InvalidOperationException or ArgumentException)
        {
            logger.LogWarning(ex, "Browser command {Kind} failed for workspace {WorkspaceId}.", command.Kind, command.WorkspaceId);
            return Fail(command, ex.Message);
        }
    }

    private static async Task<BrowserCommandResultDto> NavigateAsync(
        BrowserSessionState session,
        BrowserCommandDto command,
        CancellationToken ct)
    {
        if (string.Equals(command.Url, "about:blank", StringComparison.OrdinalIgnoreCase))
        {
            await session.Page.GoToAsync("about:blank", new NavigationOptions
            {
                Timeout = command.TimeoutMs,
                WaitUntil = [WaitUntilNavigation.Load]
            }).WaitAsync(ct);
            return Ok(command);
        }

        if (!Uri.TryCreate(command.Url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
            return Fail(command, $"Navigation target must use http or https. Received: {command.Url}");

        var page = await session.ActivatePageForUrlAsync(uri, ct);
        if (!command.ReloadIfSameUrl && string.Equals(page.Url, uri.ToString(), StringComparison.Ordinal))
            return Ok(command);

        await page.GoToAsync(uri.ToString(), new NavigationOptions
        {
            Timeout = command.TimeoutMs,
            WaitUntil = [WaitUntilNavigation.Load]
        }).WaitAsync(ct);
        return Ok(command);
    }

    private static async Task<BrowserCommandResultDto> InspectAsync(
        BrowserSessionState session,
        BrowserCommandDto command)
    {
        var data = await session.Page.EvaluateExpressionAsync<string>(BrowserScriptProvider.InspectPage);
        return new BrowserCommandResultDto(command.CommandId, true, data, null);
    }

    private static async Task<BrowserCommandResultDto> EvalAsync(
        BrowserSessionState session,
        BrowserCommandDto command,
        string script)
    {
        var data = await session.Page.EvaluateExpressionAsync<string>(script);
        return new BrowserCommandResultDto(command.CommandId, true, data, null);
    }

    private static async Task<BrowserCommandResultDto> PressAsync(
        BrowserSessionState session,
        BrowserCommandDto command)
    {
        await session.Page.Keyboard.PressAsync(command.Key!);
        return Ok(command);
    }

    private static async Task<BrowserCommandResultDto> WaitForSelectorAsync(
        BrowserSessionState session,
        BrowserCommandDto command,
        CancellationToken ct)
    {
        await session.Page.WaitForSelectorAsync(command.Selector!, new WaitForSelectorOptions
        {
            Timeout = command.TimeoutMs
        }).WaitAsync(ct);
        return Ok(command);
    }

    private static async Task<BrowserCommandResultDto> WaitForConditionAsync(
        BrowserSessionState session,
        BrowserCommandDto command,
        string conditionScript,
        CancellationToken ct)
    {
        var timeout = TimeSpan.FromMilliseconds(command.TimeoutMs);
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < timeout && !ct.IsCancellationRequested)
        {
            var met = await session.Page.EvaluateExpressionAsync<bool>(conditionScript);
            if (met) return Ok(command);
            await Task.Delay(200, ct);
        }

        return Fail(command, $"Condition not met within {command.TimeoutMs} ms.");
    }

    private static async Task<BrowserCommandResultDto> WaitForNavigationAsync(
        BrowserSessionState session,
        BrowserCommandDto command,
        CancellationToken ct)
    {
        // Brief delay to let the browser begin navigating before we watch readyState.
        await Task.Delay(200, ct);

        var timeout = TimeSpan.FromMilliseconds(command.TimeoutMs);
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < timeout && !ct.IsCancellationRequested)
        {
            var state = await session.Page.EvaluateExpressionAsync<string>(BrowserScriptProvider.CheckNavigation);
            if (state == "complete") return Ok(command);
            await Task.Delay(200, ct);
        }

        // Treat timeout as soft success — the page may still be loading but interaction can proceed.
        return Ok(command);
    }

    private static async Task<BrowserCommandResultDto> ScreenshotAsync(
        BrowserSessionState session,
        BrowserCommandDto command,
        CancellationToken ct)
    {
        var imageBytes = await session.Page.ScreenshotDataAsync(new ScreenshotOptions
        {
            Type = ScreenshotType.Png,
            FullPage = false
        }).WaitAsync(ct);

        var url = await session.Page.EvaluateExpressionAsync<string>("window.location.href");
        var width = await session.Page.EvaluateExpressionAsync<int>("window.innerWidth");
        var height = await session.Page.EvaluateExpressionAsync<int>("window.innerHeight");
        var screenshot = new BrowserScreenshotResultDto(
            url,
            "image/png",
            Convert.ToBase64String(imageBytes),
            width,
            height);
        return new BrowserCommandResultDto(command.CommandId, true, JsonSerializer.Serialize(screenshot), null);
    }

    private async Task<BrowserCommandResultDto> AttachPageStateAsync(
        BrowserCommandResultDto result,
        BrowserSessionState session,
        BrowserCommandDto command)
    {
        if (!result.Success) return result;
        try
        {
            var state = await session.Page.EvaluateExpressionAsync<string>(BrowserScriptProvider.InspectPage);
            return result with { Data = state };
        }
        catch (Exception ex) when (ex is PuppeteerException or ObjectDisposedException)
        {
            logger.LogDebug(ex, "Failed to attach page state for workspace {WorkspaceId}.", command.WorkspaceId);
            return result;
        }
    }

    private static BrowserCommandResultDto Ok(BrowserCommandDto command) =>
        new(command.CommandId, true, null, null);

    private static BrowserCommandResultDto Fail(BrowserCommandDto command, string error) =>
        new(command.CommandId, false, null, error);
}
