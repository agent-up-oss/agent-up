package dev.agentup.jetbrains.cli;

import java.nio.file.Path;
import java.time.Duration;
import java.util.List;

public record CliCommand(
    String executable,
    List<String> arguments,
    Path workingDirectory,
    Duration timeout
) {
}
