package dev.agentup.jetbrains.cli;

public record NextCommitResponse(
    boolean staged,
    boolean empty,
    String slice,
    String message,
    int remainingCount
) {
}
