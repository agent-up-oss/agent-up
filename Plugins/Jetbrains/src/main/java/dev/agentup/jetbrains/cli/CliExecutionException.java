package dev.agentup.jetbrains.cli;

public final class CliExecutionException extends RuntimeException {
    public CliExecutionException(String message) {
        super(message);
    }

    public CliExecutionException(String message, Throwable cause) {
        super(message, cause);
    }
}
