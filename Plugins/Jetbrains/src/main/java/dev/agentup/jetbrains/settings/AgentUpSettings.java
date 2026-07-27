package dev.agentup.jetbrains.settings;

import com.intellij.openapi.components.PersistentStateComponent;
import com.intellij.openapi.components.Service;
import com.intellij.openapi.components.State;
import com.intellij.openapi.components.Storage;
import org.jetbrains.annotations.NotNull;

@Service(Service.Level.APP)
@State(name = "AgentUpSettings", storages = @Storage("agent-up.xml"))
public final class AgentUpSettings implements PersistentStateComponent<AgentUpSettings.State> {
    private State state = new State();

    @Override
    public @NotNull State getState() {
        return state;
    }

    @Override
    public void loadState(@NotNull State state) {
        this.state = state;
    }

    public static final class State {
        public String executablePath = "agent-up";
        public int pollIntervalSeconds = 5;
        public int statusTimeoutSeconds = 5;
        public int operationTimeoutSeconds = 60;
    }
}
