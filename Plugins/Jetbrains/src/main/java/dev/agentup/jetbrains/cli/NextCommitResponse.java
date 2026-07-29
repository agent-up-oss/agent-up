package dev.agentup.jetbrains.cli;

public record NextCommitResponse(
    boolean staged,
    boolean empty,
    boolean blocked,
    String slice,
    String message,
    int remainingCount
) {
}
