package dev.agentup.jetbrains.queue;

import com.intellij.ide.ActivityTracker;
import com.intellij.openapi.Disposable;
import com.intellij.openapi.application.ApplicationManager;
import com.intellij.openapi.components.Service;
import com.intellij.openapi.project.Project;
import com.intellij.openapi.vcs.changes.ChangeList;
import com.intellij.openapi.vcs.changes.ChangeListListener;
import com.intellij.util.concurrency.AppExecutorUtil;
import dev.agentup.jetbrains.cli.CliExecutionException;
import dev.agentup.jetbrains.settings.AgentUpSettings;
import org.jetbrains.annotations.NotNull;

import java.util.concurrent.ScheduledFuture;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicBoolean;

@Service(Service.Level.PROJECT)
public final class QueueRefreshCoordinator implements Disposable {
    private final Project project;
    private final AtomicBoolean refreshRunning = new AtomicBoolean(false);
    private final AtomicBoolean pollingStarted = new AtomicBoolean(false);
    private volatile QueueState state = QueueState.loading();
    private ScheduledFuture<?> poller;

    public QueueRefreshCoordinator(Project project) {
        this.project = project;
        project.getMessageBus().connect(this).subscribe(ChangeListListener.TOPIC, new ChangeListListener() {
            @Override
            public void changeListUpdateDone() {
                refresh();
            }

            @Override
            public void changedFileStatusChanged() {
                refresh();
            }

            @Override
            public void changeListChanged(@NotNull ChangeList list) {
                refresh();
            }
        });
    }

    public QueueState getState() {
        return state;
    }

    public void startPollingIfNeeded() {
        if (!pollingStarted.compareAndSet(false, true)) {
            return;
        }

        refresh();
        scheduleNextPoll();
    }

    public void refresh() {
        if (project.isDisposed() || !refreshRunning.compareAndSet(false, true)) {
            return;
        }

        AppExecutorUtil.getAppExecutorService().execute(() -> {
            try {
                updateState(project.getService(QueueService.class).getQueueSize());
            } catch (CliExecutionException ex) {
                updateState(QueueState.offline(ApplicationManager.getApplication().getService(AgentUpSettings.class).getState().executablePath));
            } catch (Throwable ex) {
                updateState(QueueState.failed(ex.getMessage() == null ? "Agent-Up queue refresh failed." : ex.getMessage()));
            } finally {
                refreshRunning.set(false);
            }
        });
    }

    private void updateState(QueueState next) {
        state = next;
        ApplicationManager.getApplication().invokeLater(() -> ActivityTracker.getInstance().inc());
    }

    private void scheduleNextPoll() {
        int interval = Math.max(2, ApplicationManager.getApplication().getService(AgentUpSettings.class).getState().pollIntervalSeconds);
        poller = AppExecutorUtil.getAppScheduledExecutorService().schedule(() -> {
            if (project.isDisposed() || !pollingStarted.get()) {
                return;
            }

            refresh();
            scheduleNextPoll();
        }, interval, TimeUnit.SECONDS);
    }

    @Override
    public void dispose() {
        pollingStarted.set(false);
        if (poller != null) {
            poller.cancel(true);
            poller = null;
        }
    }
}
