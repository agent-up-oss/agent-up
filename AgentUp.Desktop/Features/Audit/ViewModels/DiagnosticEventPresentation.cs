using AgentUp.Desktop.Features.Audit.DTOs;

namespace AgentUp.Desktop.Features.Audit.ViewModels;

internal static class DiagnosticEventPresentation
{
    internal const string SuccessColor = "#00b850";
    internal const string ErrorColor = "#e48989";
    internal const string WarningColor = "#d4a34a";
    internal const string NeutralColor = "#8aa497";
    internal const string MessageDefaultColor = "#aebcb3";

    internal static DiagnosticPresentation Present(ApplicationAuditEventDto dto)
    {
        if (IsConsoleLine(dto))
            return PresentConsoleLine(dto);

        if (string.Equals(dto.Kind, "health", StringComparison.OrdinalIgnoreCase))
            return PresentHealth(dto);

        var message = BuildMessage(dto);
        var messageTone = ClassifyMessage(message);

        if (IsFailureOutcome(dto.Outcome))
            return new DiagnosticPresentation(CategoryLabel(dto), ErrorColor, message, ErrorColor);

        if (IsWarningOutcome(dto.Outcome) || messageTone == DiagnosticMessageTone.Warning)
            return new DiagnosticPresentation(CategoryLabel(dto), WarningColor, message, WarningColor);

        if (IsPositiveOutcome(dto.Outcome) && messageTone != DiagnosticMessageTone.Error)
            return new DiagnosticPresentation(CategoryLabel(dto), SuccessColor, message, MessageDefaultColor);

        return messageTone switch
        {
            DiagnosticMessageTone.Error => new DiagnosticPresentation(CategoryLabel(dto), ErrorColor, message, ErrorColor),
            DiagnosticMessageTone.Warning => new DiagnosticPresentation(CategoryLabel(dto), WarningColor, message, WarningColor),
            _ => new DiagnosticPresentation(CategoryLabel(dto), NeutralColor, message, MessageDefaultColor)
        };
    }

    private static DiagnosticPresentation PresentConsoleLine(ApplicationAuditEventDto dto)
    {
        var stream = GetDetail(dto, "stream");
        var message = GetDetail(dto, "message") ?? string.Empty;
        var category = string.Equals(stream, "stderr", StringComparison.OrdinalIgnoreCase)
            ? "Stderr"
            : "Stdout";

        if (string.Equals(stream, "stderr", StringComparison.OrdinalIgnoreCase))
            return new DiagnosticPresentation(category, ErrorColor, message, ErrorColor);

        return ClassifyMessage(message) switch
        {
            DiagnosticMessageTone.Error => new DiagnosticPresentation(category, ErrorColor, message, ErrorColor),
            DiagnosticMessageTone.Warning => new DiagnosticPresentation(category, WarningColor, message, WarningColor),
            _ => new DiagnosticPresentation(category, NeutralColor, message, MessageDefaultColor)
        };
    }

    private static DiagnosticPresentation PresentHealth(ApplicationAuditEventDto dto)
    {
        var message = BuildHealthMessage(dto);
        var isHealthy = IsHealthyHealthEvent(dto);
        return isHealthy
            ? new DiagnosticPresentation("Health", SuccessColor, message, MessageDefaultColor)
            : new DiagnosticPresentation("Health", ErrorColor, message, ErrorColor);
    }

    private static bool IsHealthyHealthEvent(ApplicationAuditEventDto dto)
        => string.Equals(dto.Outcome, "healthy", StringComparison.OrdinalIgnoreCase)
           || string.Equals(GetDetail(dto, "state"), "Healthy", StringComparison.OrdinalIgnoreCase);

    private static string BuildHealthMessage(ApplicationAuditEventDto dto)
    {
        var appName = GetDetail(dto, "appName") ?? GetDetail(dto, "applicationName") ?? GetDetail(dto, "application");
        var port = GetDetail(dto, "port");
        var state = GetDetail(dto, "state") ?? dto.Outcome;
        var url = GetDetail(dto, "url");

        if (appName is not null && port is not null && url is not null)
            return $"{appName}:{port} {state} ({url})";

        if (appName is not null && port is not null)
            return $"{appName}:{port} {state}";

        return string.IsNullOrWhiteSpace(state) ? dto.Action : state;
    }

    private static string CategoryLabel(ApplicationAuditEventDto dto)
        => dto.Kind.ToLowerInvariant() switch
        {
            "frontend" => "Frontend",
            "application" => "Console",
            "health" => "Health",
            "metrics" => "Metrics",
            "browser" => "Browser",
            "workspace" => "Workspace",
            "stream" => "Stream",
            _ => string.IsNullOrWhiteSpace(dto.Kind) ? dto.Action : char.ToUpperInvariant(dto.Kind[0]) + dto.Kind[1..].ToLowerInvariant()
        };

    private static string BuildMessage(ApplicationAuditEventDto dto)
    {
        var message = GetDetail(dto, "message");
        if (!string.IsNullOrWhiteSpace(message))
            return message;

        return string.Join(" · ", dto.Details
            .Where(pair => !IsHiddenDetailKey(pair.Key))
            .Select(pair => $"{pair.Key}: {pair.Value}"));
    }

    private static bool IsHiddenDetailKey(string key)
        => key.Equals("application", StringComparison.OrdinalIgnoreCase)
           || key.Equals("applicationName", StringComparison.OrdinalIgnoreCase)
           || key.Equals("appName", StringComparison.OrdinalIgnoreCase)
           || key.Equals("stream", StringComparison.OrdinalIgnoreCase);

    private static string? GetDetail(ApplicationAuditEventDto dto, string key)
        => dto.Details.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    private static bool IsConsoleLine(ApplicationAuditEventDto dto)
        => string.Equals(dto.Action, "application_console_line", StringComparison.Ordinal);

    private static bool IsFailureOutcome(string outcome)
        => outcome.Contains("fail", StringComparison.OrdinalIgnoreCase)
           || outcome.Contains("error", StringComparison.OrdinalIgnoreCase)
           || outcome.Equals("unhealthy", StringComparison.OrdinalIgnoreCase);

    private static bool IsWarningOutcome(string outcome)
        => outcome.Contains("warn", StringComparison.OrdinalIgnoreCase)
           || outcome.Contains("degraded", StringComparison.OrdinalIgnoreCase);

    private static bool IsPositiveOutcome(string outcome)
        => outcome.Contains("success", StringComparison.OrdinalIgnoreCase)
           || outcome.Contains("healthy", StringComparison.OrdinalIgnoreCase)
           || outcome.Contains("resolved", StringComparison.OrdinalIgnoreCase);

    private static DiagnosticMessageTone ClassifyMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return DiagnosticMessageTone.Neutral;

        if (ContainsAny(message, "[ERROR]", " ERROR ", "FAIL", "Exception", "brokers are down"))
            return DiagnosticMessageTone.Error;

        if (ContainsAny(message, "[WARN", " WARNING ", "Warn:", "warning"))
            return DiagnosticMessageTone.Warning;

        return DiagnosticMessageTone.Neutral;
    }

    private static bool ContainsAny(string message, params string[] needles)
        => needles.Any(needle => message.Contains(needle, StringComparison.OrdinalIgnoreCase));
}
