namespace AgentUp.TestAgents.Features.Scripted.Models;

public enum ScriptedTurnKind
{
    Complete,
    EndTurn,
    Permission,
    ToolCall,
    ToolError,
    RateLimited,
    Crash,
    Empty,
    Whitespace,
    TruncatedJson,
    UnknownTool,
    HugeArgs,
    HoldPipe,
    InvalidUtf8,
    DuplicatePermission,
    OutOfOrder
}
