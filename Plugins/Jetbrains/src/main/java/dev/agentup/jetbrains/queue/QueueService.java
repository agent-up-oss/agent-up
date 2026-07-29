package dev.agentup.jetbrains.queue;

import com.intellij.openapi.application.ApplicationManager;
import com.intellij.openapi.components.Service;
import com.intellij.openapi.project.Project;
import dev.agentup.jetbrains.cli.CliCommand;
import dev.agentup.jetbrains.cli.CliExecutionException;
import dev.agentup.jetbrains.cli.CliExecutionResult;
import dev.agentup.jetbrains.cli.CliExecutionService;
import dev.agentup.jetbrains.cli.CliJsonParser;
import dev.agentup.jetbrains.cli.CliJsonParseException;
import dev.agentup.jetbrains.cli.NextCommitResponse;
import dev.agentup.jetbrains.settings.AgentUpSettings;

import java.nio.file.Path;
import java.time.Duration;
import java.util.List;

@Service(Service.Level.PROJECT)
public final class QueueService {
    private final Project project;
    private final CliJsonParser parser = new CliJsonParser();

    public QueueService(Project project) {
        this.project = project;
    }

    public QueueState getQueueSize() {
        Path repository = repositoryPath();
        if (repository == null) {
            return QueueState.failed("No local project path is available.");
        }

        AgentUpSettings.State settings = ApplicationManager.getApplication().getService(AgentUpSettings.class).getState();
        CliExecutionResult result;
        try {
            result = project.getService(CliExecutionService.class).execute(
                project,
                new CliCommand(
                    settings.executablePath,
                    List.of("commits", "status", "--format", "json"),
                    repository,
                    Duration.ofSeconds(settings.statusTimeoutSeconds)
                )
            );
        } catch (CliExecutionException ex) {
            return QueueState.offline(settings.executablePath);
        }

        if (!result.succeeded()) {
            return result.exitCode() == -1
                ? QueueState.offline(settings.executablePath)
                : QueueState.failed(parser.parseError(result.stdout(), result.stderr()));
        }

        try {
            var status = parser.parseStatus(result.stdout());
            return QueueState.available(status.count(), status.messages(), status.operationKind(), status.operationBlocking());
        } catch (CliJsonParseException ex) {
            return QueueState.failed(ex.getMessage());
        }
    }

    public NextCommitResponse runNext() {
        Path repository = repositoryPath();
        if (repository == null) {
            throw new CliExecutionException("No local project path is available.");
        }

        AgentUpSettings.State settings = ApplicationManager.getApplication().getService(AgentUpSettings.class).getState();
        CliExecutionResult result = project.getService(CliExecutionService.class).execute(
            project,
            new CliCommand(
                settings.executablePath,
                List.of("commits", "next", "--format", "json"),
                repository,
                Duration.ofSeconds(settings.operationTimeoutSeconds)
            )
        );

        try {
            NextCommitResponse response = parser.parseNext(result.stdout());
            if (!result.succeeded() && !response.blocked()) {
                throw new CliExecutionException(parser.parseError(result.stdout(), result.stderr()));
            }

            return response;
        } catch (CliJsonParseException ex) {
            if (!result.succeeded()) {
                throw new CliExecutionException(parser.parseError(result.stdout(), result.stderr()), ex);
            }

            throw new CliExecutionException(ex.getMessage(), ex);
        }
    }

    private Path repositoryPath() {
        String basePath = project.getBasePath();
        return basePath == null ? null : Path.of(basePath);
    }
}
