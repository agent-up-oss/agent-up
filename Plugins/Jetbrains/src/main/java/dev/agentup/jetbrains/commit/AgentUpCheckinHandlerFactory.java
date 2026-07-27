package dev.agentup.jetbrains.commit;

import com.intellij.openapi.project.Project;
import com.intellij.openapi.vcs.CheckinProjectPanel;
import com.intellij.openapi.vcs.checkin.CheckinHandler;
import com.intellij.openapi.vcs.checkin.CheckinHandlerFactory;
import com.intellij.openapi.vcs.changes.CommitContext;
import dev.agentup.jetbrains.queue.QueueRefreshCoordinator;
import org.jetbrains.annotations.NotNull;

public final class AgentUpCheckinHandlerFactory extends CheckinHandlerFactory {
    @Override
    public @NotNull CheckinHandler createHandler(@NotNull CheckinProjectPanel panel, @NotNull CommitContext commitContext) {
        Project project = panel.getProject();
        return new CheckinHandler() {
            @Override
            public void checkinSuccessful() {
                project.getService(QueueRefreshCoordinator.class).refresh();
            }
        };
    }
}
