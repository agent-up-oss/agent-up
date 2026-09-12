namespace AgentUp.Desktop.Features.Agents.ViewModels;
public sealed record AgentChatItemViewModel(string Role, string Text, string? Status = null, string? ToolCallId = null);
