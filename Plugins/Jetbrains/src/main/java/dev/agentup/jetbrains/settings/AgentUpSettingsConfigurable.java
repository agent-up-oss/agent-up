package dev.agentup.jetbrains.settings;

import com.intellij.openapi.application.ApplicationManager;
import com.intellij.openapi.options.Configurable;
import com.intellij.ui.components.JBLabel;
import com.intellij.ui.components.JBTextField;
import com.intellij.util.ui.FormBuilder;
import org.jetbrains.annotations.Nls;
import org.jetbrains.annotations.Nullable;

import javax.swing.JComponent;
import javax.swing.JPanel;
import javax.swing.JSpinner;
import javax.swing.SpinnerNumberModel;

public final class AgentUpSettingsConfigurable implements Configurable {
    private final AgentUpSettings settings = ApplicationManager.getApplication().getService(AgentUpSettings.class);
    private SettingsComponent component;

    @Override
    public @Nls String getDisplayName() {
        return "Agent-Up";
    }

    @Override
    public @Nullable JComponent createComponent() {
        component = new SettingsComponent(settings.getState());
        return component.panel;
    }

    @Override
    public boolean isModified() {
        if (component == null) {
            return false;
        }

        AgentUpSettings.State state = settings.getState();
        return !component.executablePath().equals(state.executablePath)
            || component.pollIntervalSeconds() != state.pollIntervalSeconds
            || component.statusTimeoutSeconds() != state.statusTimeoutSeconds
            || component.operationTimeoutSeconds() != state.operationTimeoutSeconds;
    }

    @Override
    public void apply() {
        if (component == null) {
            return;
        }

        AgentUpSettings.State state = settings.getState();
        state.executablePath = component.executablePath();
        state.pollIntervalSeconds = component.pollIntervalSeconds();
        state.statusTimeoutSeconds = component.statusTimeoutSeconds();
        state.operationTimeoutSeconds = component.operationTimeoutSeconds();
    }

    @Override
    public void reset() {
        if (component != null) {
            component.reset(settings.getState());
        }
    }

    @Override
    public void disposeUIResources() {
        component = null;
    }

    private static final class SettingsComponent {
        private final JBTextField executable = new JBTextField();
        private final JSpinner pollInterval = new JSpinner(new SpinnerNumberModel(5, 2, 300, 1));
        private final JSpinner statusTimeout = new JSpinner(new SpinnerNumberModel(5, 1, 120, 1));
        private final JSpinner operationTimeout = new JSpinner(new SpinnerNumberModel(60, 1, 600, 1));
        private final JPanel panel;

        private SettingsComponent(AgentUpSettings.State state) {
            panel = FormBuilder.createFormBuilder()
                .addLabeledComponent(new JBLabel("CLI executable:"), executable, 1, false)
                .addLabeledComponent(new JBLabel("Queue refresh interval (seconds):"), pollInterval, 1, false)
                .addLabeledComponent(new JBLabel("Status timeout (seconds):"), statusTimeout, 1, false)
                .addLabeledComponent(new JBLabel("Operation timeout (seconds):"), operationTimeout, 1, false)
                .addComponentFillVertically(new JPanel(), 0)
                .getPanel();
            reset(state);
        }

        private String executablePath() {
            String value = executable.getText().trim();
            return value.isEmpty() ? "agentup" : value;
        }

        private int pollIntervalSeconds() {
            return (Integer)pollInterval.getValue();
        }

        private int statusTimeoutSeconds() {
            return (Integer)statusTimeout.getValue();
        }

        private int operationTimeoutSeconds() {
            return (Integer)operationTimeout.getValue();
        }

        private void reset(AgentUpSettings.State state) {
            executable.setText(state.executablePath);
            pollInterval.setValue(state.pollIntervalSeconds);
            statusTimeout.setValue(state.statusTimeoutSeconds);
            operationTimeout.setValue(state.operationTimeoutSeconds);
        }
    }
}
