namespace AgentUp.Server.Tests.Fake;

/// <summary>Mirrors AgentUp.TestAgents ScriptedTurnKind so Server tests never reference that project.</summary>
internal enum ScriptedTurnKind
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
