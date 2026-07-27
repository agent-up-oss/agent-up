package dev.agentup.jetbrains.notifications;

import com.intellij.notification.NotificationGroupManager;
import com.intellij.notification.NotificationType;
import com.intellij.openapi.components.Service;
import com.intellij.openapi.project.Project;

@Service(Service.Level.PROJECT)
public final class PluginNotificationService {
    public void info(Project project, String message) {
        notify(project, message, NotificationType.INFORMATION);
    }

    public void warn(Project project, String message) {
        notify(project, message, NotificationType.WARNING);
    }

    public void error(Project project, String message) {
        notify(project, message, NotificationType.ERROR);
    }

    private static void notify(Project project, String message, NotificationType type) {
        NotificationGroupManager.getInstance()
            .getNotificationGroup("Agent-Up")
            .createNotification(message, type)
            .notify(project);
    }
}
