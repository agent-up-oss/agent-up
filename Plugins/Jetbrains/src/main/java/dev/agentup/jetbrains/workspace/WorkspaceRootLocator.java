package dev.agentup.jetbrains.workspace;

import java.nio.file.Files;
import java.nio.file.Path;

public final class WorkspaceRootLocator {
    private WorkspaceRootLocator() {
    }

    public static Path find(Path startingDirectory) {
        Path directory = startingDirectory.toAbsolutePath().normalize();
        while (directory != null) {
            if (Files.isRegularFile(directory.resolve("agent-up.json"))) {
                return directory;
            }

            directory = directory.getParent();
        }

        return null;
    }
}
