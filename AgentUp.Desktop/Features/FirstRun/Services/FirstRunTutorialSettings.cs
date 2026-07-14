namespace AgentUp.Desktop.Features.FirstRun.Services;

public sealed record FirstRunTutorialSettings(
    bool TutorialCompleted,
    bool TutorialSkipped,
    int CompletedStep);
