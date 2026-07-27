package dev.agentup.jetbrains.actions;

import com.intellij.openapi.actionSystem.ActionUpdateThread;
import com.intellij.openapi.actionSystem.AnActionEvent;
import com.intellij.openapi.application.ApplicationManager;
import com.intellij.openapi.progress.ProgressIndicator;
import com.intellij.openapi.progress.Task;
import com.intellij.openapi.project.DumbAwareAction;
import com.intellij.openapi.project.Project;
import com.intellij.openapi.vcs.CheckinProjectPanel;
import com.intellij.openapi.vcs.changes.VcsDirtyScopeManager;
import com.intellij.openapi.vcs.ui.Refreshable;
import dev.agentup.jetbrains.cli.CliExecutionException;
import dev.agentup.jetbrains.cli.NextCommitResponse;
import dev.agentup.jetbrains.commit.CommitMessageController;
import dev.agentup.jetbrains.notifications.PluginNotificationService;
import dev.agentup.jetbrains.queue.QueueRefreshCoordinator;
import dev.agentup.jetbrains.queue.QueueService;
import dev.agentup.jetbrains.queue.QueueState;
import org.jetbrains.annotations.NotNull;

public final class QueueAction extends DumbAwareAction {
    @Override
    public @NotNull ActionUpdateThread getActionUpdateThread() {
        return ActionUpdateThread.BGT;
    }

    @Override
    public void update(@NotNull AnActionEvent event) {
        Project project = event.getProject();
        if (project == null || project.isDisposed()) {
            event.getPresentation().setEnabled(false);
            event.getPresentation().setText("Queue: Offline");
            return;
        }

        QueueRefreshCoordinator coordinator = project.getService(QueueRefreshCoordinator.class);
        coordinator.startPollingIfNeeded();
        QueueState state = coordinator.getState();
        event.getPresentation().setText(labelFor(state));
        event.getPresentation().setEnabled(state.getKind() == QueueState.Kind.AVAILABLE && state.getCount() > 0);
    }

    @Override
    public void actionPerformed(@NotNull AnActionEvent event) {
        Project project = event.getProject();
        if (project == null) {
            return;
        }

        Object panelValue = event.getData(Refreshable.PANEL_KEY);
        CheckinProjectPanel panel = panelValue instanceof CheckinProjectPanel checkinPanel ? checkinPanel : null;

        new Task.Backgroundable(project, "Agent-Up Commit Queue", true) {
            @Override
            public void run(@NotNull ProgressIndicator indicator) {
                indicator.setText("Running agentup commits next");
                try {
                    NextCommitResponse result = project.getService(QueueService.class).runNext();
                    ApplicationManager.getApplication().invokeLater(() -> handleResult(project, panel, result));
                } catch (CliExecutionException ex) {
                    ApplicationManager.getApplication().invokeLater(() -> {
                        if (!project.isDisposed()) {
                            project.getService(PluginNotificationService.class).error(project, ex.getMessage());
                            project.getService(QueueRefreshCoordinator.class).refresh();
                        }
                    });
                }
            }
        }.queue();
    }

    private static void handleResult(Project project, CheckinProjectPanel panel, NextCommitResponse result) {
        if (project.isDisposed()) {
            return;
        }

        if (result.empty()) {
            project.getService(PluginNotificationService.class).info(project, "Agent-Up commit queue is empty.");
        } else if (result.message() == null || result.message().isBlank()) {
            project.getService(PluginNotificationService.class).warn(project, "Agent-Up did not return a commit message.");
        } else {
            boolean replaced = new CommitMessageController(panel).replaceMessage(result.message());
            if (!replaced) {
                project.getService(PluginNotificationService.class).warn(project, "Open the Commit tool window to insert the Agent-Up commit message.");
            }

            VcsDirtyScopeManager.getInstance(project).markEverythingDirty();
        }

        project.getService(QueueRefreshCoordinator.class).refresh();
    }

    private static String labelFor(QueueState state) {
        return switch (state.getKind()) {
            case LOADING -> "Queue: ...";
            case OFFLINE -> "Queue: Offline";
            case AVAILABLE -> "Queue: " + state.getCount();
            case FAILED -> "Queue: Error";
        };
    }
}
