package dev.agentup.jetbrains.cli;

import com.intellij.execution.configurations.GeneralCommandLine;
import com.intellij.execution.process.CapturingProcessHandler;
import com.intellij.execution.process.ProcessOutput;
import com.intellij.openapi.components.Service;
import com.intellij.openapi.diagnostic.Logger;
import com.intellij.openapi.project.Project;

import java.nio.charset.StandardCharsets;

@Service(Service.Level.PROJECT)
public final class CliExecutionService {
    private static final Logger LOG = Logger.getInstance(CliExecutionService.class);
    private static final int MAX_OUTPUT_LENGTH = 128 * 1024;

    public CliExecutionResult execute(Project project, CliCommand command) {
        CliExecutableCommand executableCommand = CliExecutableCommand.parse(command.executable());
        GeneralCommandLine commandLine = new GeneralCommandLine(executableCommand.executable())
            .withCharset(StandardCharsets.UTF_8)
            .withRedirectErrorStream(false);

        commandLine.addParameters(executableCommand.arguments());
        commandLine.addParameters(command.arguments());
        if (command.workingDirectory() != null) {
            commandLine.withWorkDirectory(command.workingDirectory().toFile());
        }

        try {
            CapturingProcessHandler handler = new CapturingProcessHandler(commandLine);
            int timeoutMillis = (int)Math.max(1, command.timeout().toMillis());
            ProcessOutput output = handler.runProcess(timeoutMillis);
            return new CliExecutionResult(output.getExitCode(), trimOutput(output.getStdout()), trimOutput(output.getStderr()));
        } catch (Exception ex) {
            LOG.warn("Agent-Up CLI command failed for " + project.getName(), ex);
            throw new CliExecutionException("Agent-Up CLI could not be executed.", ex);
        }
    }

    private static String trimOutput(String output) {
        if (output.length() <= MAX_OUTPUT_LENGTH) {
            return output.trim();
        }

        return output.substring(0, MAX_OUTPUT_LENGTH).trim();
    }
}
