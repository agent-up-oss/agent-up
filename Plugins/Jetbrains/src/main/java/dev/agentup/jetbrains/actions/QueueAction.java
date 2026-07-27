package dev.agentup.jetbrains.actions;

import com.intellij.openapi.actionSystem.ActionUpdateThread;
import com.intellij.openapi.actionSystem.AnActionEvent;
import com.intellij.openapi.actionSystem.ActionPlaces;
import com.intellij.openapi.actionSystem.impl.ActionButton;
import com.intellij.openapi.application.ApplicationManager;
import com.intellij.ide.HelpTooltip;
import com.intellij.openapi.progress.ProgressIndicator;
import com.intellij.openapi.progress.Task;
import com.intellij.openapi.project.DumbAwareAction;
import com.intellij.openapi.project.Project;
import com.intellij.openapi.util.IconLoader;
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

import javax.swing.Icon;
import java.util.List;

public final class QueueAction extends DumbAwareAction {
    private static final String ACTION_TEXT = "Agent-Up Queue";
    private static final Icon ACTION_ICON = IconLoader.getIcon("/icons/agent-up.svg", QueueAction.class);
    private static final Icon EMPTY_ICON = IconLoader.getIcon("/icons/agent-up-grey.svg", QueueAction.class);
    private static final Icon OFFLINE_ICON = IconLoader.getIcon("/icons/agent-up-red.svg", QueueAction.class);

    public QueueAction() {
        getTemplatePresentation().setIcon(EMPTY_ICON);
    }

    @Override
    public @NotNull ActionUpdateThread getActionUpdateThread() {
        return ActionUpdateThread.BGT;
    }

    @Override
    public void update(@NotNull AnActionEvent event) {
        Project project = event.getProject();
        if (project == null || project.isDisposed()) {
            event.getPresentation().setEnabled(false);
            event.getPresentation().setText(textFor(event));
            event.getPresentation().setIcon(OFFLINE_ICON);
            event.getPresentation().setDisabledIcon(OFFLINE_ICON);
            event.getPresentation().setDescription("agent-up is not available");
            event.getPresentation().putClientProperty(ActionButton.CUSTOM_HELP_TOOLTIP, new HelpTooltip().setTitle("agent-up is not available"));
            return;
        }

        QueueRefreshCoordinator coordinator = project.getService(QueueRefreshCoordinator.class);
        coordinator.startPollingIfNeeded();
        QueueState state = coordinator.getState();
        event.getPresentation().setText(textFor(event));
        event.getPresentation().setIcon(iconFor(state));
        event.getPresentation().setDisabledIcon(iconFor(state));
        String description = descriptionFor(state);
        event.getPresentation().setDescription(description);
        event.getPresentation().putClientProperty(ActionButton.CUSTOM_HELP_TOOLTIP, helpTooltipFor(state));
        event.getPresentation().setEnabled(state.getKind() == QueueState.Kind.AVAILABLE && state.getCount() > 0);
    }

    @Override
    public void actionPerformed(@NotNull AnActionEvent event) {
        Project project = event.getProject();
        if (project == null) {
            return;
        }

        QueueState state = project.getService(QueueRefreshCoordinator.class).getState();
        if (state.getKind() != QueueState.Kind.AVAILABLE || state.getCount() == 0) {
            return;
        }

        Object panelValue = event.getData(Refreshable.PANEL_KEY);
        CheckinProjectPanel panel = panelValue instanceof CheckinProjectPanel checkinPanel ? checkinPanel : null;

        new Task.Backgroundable(project, "Agent-Up Commit Queue", true) {
            @Override
            public void run(@NotNull ProgressIndicator indicator) {
                indicator.setText("Running agent-up commits next");
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

    private static String descriptionFor(QueueState state) {
        return switch (state.getKind()) {
            case LOADING -> "Agent-Up commit queue is loading";
            case OFFLINE -> executableUnavailable(state);
            case AVAILABLE -> state.getCount() > 0
                ? queueTooltip(state.getMessages())
                : "Agent-Up commits queue empty";
            case FAILED -> "Agent-Up commit queue failed to load";
        };
    }

    private static Icon iconFor(QueueState state) {
        return switch (state.getKind()) {
            case OFFLINE, FAILED -> OFFLINE_ICON;
            case AVAILABLE -> state.getCount() > 0 ? ACTION_ICON : EMPTY_ICON;
            case LOADING -> EMPTY_ICON;
        };
    }

    private static String executableUnavailable(QueueState state) {
        String executable = state.getExecutable() == null || state.getExecutable().isBlank()
            ? "agent-up"
            : state.getExecutable();
        return executable + " is not available";
    }

    private static String queueTooltip(List<String> messages) {
        if (messages.isEmpty()) {
            return "Agent-Up commit queue has queued entries";
        }

        StringBuilder tooltip = new StringBuilder("<html>Agent-Up commit queue:");
        for (String message : messages) {
            tooltip.append("<br>- ").append(escapeHtml(message));
        }

        tooltip.append("</html>");
        return tooltip.toString();
    }

    private static String escapeHtml(String value) {
        return value
            .replace("&", "&amp;")
            .replace("<", "&lt;")
            .replace(">", "&gt;")
            .replace("\"", "&quot;");
    }

    private static String textFor(AnActionEvent event) {
        String place = event.getPlace();
        return ActionPlaces.MAIN_MENU.equals(place) || place.toLowerCase().contains("popup")
            ? ACTION_TEXT
            : "";
    }

    private static HelpTooltip helpTooltipFor(QueueState state) {
        return switch (state.getKind()) {
            case LOADING -> new HelpTooltip().setTitle("Agent-Up commit queue is loading");
            case OFFLINE -> new HelpTooltip().setTitle(executableUnavailable(state));
            case FAILED -> new HelpTooltip().setTitle("Agent-Up commit queue failed to load");
            case AVAILABLE -> state.getCount() > 0
                ? new HelpTooltip().setTitle("Agent-Up commit queue").setDescription(queueTooltipDescription(state.getMessages()))
                : new HelpTooltip().setTitle("Agent-Up commits queue empty");
        };
    }

    private static String queueTooltipDescription(List<String> messages) {
        if (messages.isEmpty()) {
            return "Queued entries are available.";
        }

        StringBuilder tooltip = new StringBuilder();
        for (int index = 0; index < messages.size(); index++) {
            if (index > 0) {
                tooltip.append("<br>");
            }
            tooltip.append("- ").append(escapeHtml(messages.get(index)));
        }
        return tooltip.toString();
    }
}
