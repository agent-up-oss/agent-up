package dev.agentup.jetbrains.commit;

import com.intellij.openapi.vcs.CheckinProjectPanel;

public final class CommitMessageController {
    private final CheckinProjectPanel panel;

    public CommitMessageController(CheckinProjectPanel panel) {
        this.panel = panel;
    }

    public boolean replaceMessage(String message) {
        if (panel == null) {
            return false;
        }

        panel.setCommitMessage(message);
        return true;
    }
}
