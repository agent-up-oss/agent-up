package dev.agentup.jetbrains.workspace;

import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNull;

final class WorkspaceRootLocatorTest {
    @TempDir
    Path temporaryDirectory;

    @Test
    void findsAgentUpWorkspaceAboveNestedProjectDirectory() throws IOException {
        Files.writeString(temporaryDirectory.resolve("agent-up.json"), "{}");
        Path nestedProject = Files.createDirectories(temporaryDirectory.resolve("src/plugin"));

        assertEquals(temporaryDirectory, WorkspaceRootLocator.find(nestedProject));
    }

    @Test
    void returnsNullAfterSearchingAllParents() throws IOException {
        Path nestedProject = Files.createDirectories(temporaryDirectory.resolve("src/plugin"));

        assertNull(WorkspaceRootLocator.find(nestedProject));
    }
}
