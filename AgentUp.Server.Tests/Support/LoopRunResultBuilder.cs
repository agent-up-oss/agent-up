using AgentUp.Server.Features.Agents.DTOs;

namespace AgentUp.Server.Tests.Support;

internal sealed class LoopRunResultBuilder
{
    private LoopTerminal _terminal = LoopTerminal.Completed;
    private string? _stopReason = "end_turn";
    private int _iterations = 1;
    private IReadOnlyList<string> _rules = ["allow-all"];
    private bool _drained = true;
    private bool _gateReleased = true;
    private int _transcript;
    private int _context = 32;
    private bool _completed = true;
    private string? _block;
    private uint _seed = 42;

    public LoopRunResultBuilder Terminal(LoopTerminal terminal)
    {
        _terminal = terminal;
        if (terminal != LoopTerminal.Completed)
            _completed = false;
        if (terminal == LoopTerminal.BlockedPolicy)
            _block ??= "blocked:policy";
        return this;
    }

    public LoopRunResultBuilder StopReason(string? reason)
    {
        _stopReason = reason;
        return this;
    }

    public LoopRunResultBuilder PermissionsHeld()
    {
        _drained = false;
        return this;
    }

    public LoopRunResultBuilder GateHeld()
    {
        _gateReleased = false;
        return this;
    }

    public LoopRunResultBuilder WithoutRuleIds()
    {
        _rules = [""];
        return this;
    }

    public LoopRunResultBuilder Transcript(int events)
    {
        _transcript = events;
        return this;
    }

    public LoopRunResult Build() => new(
        _terminal,
        _stopReason,
        _iterations,
        _rules,
        _drained,
        _gateReleased,
        _transcript,
        _context,
        _completed,
        _block,
        _seed);
}
