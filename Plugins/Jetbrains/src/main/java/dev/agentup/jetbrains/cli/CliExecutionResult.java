package dev.agentup.jetbrains.cli;

public record CliExecutionResult(int exitCode, String stdout, String stderr) {
    public boolean succeeded() {
        return exitCode == 0;
    }
}
