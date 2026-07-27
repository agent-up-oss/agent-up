package dev.agentup.jetbrains.cli;

import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

final class CliJsonParserTest {
    private final CliJsonParser parser = new CliJsonParser();

    @Test
    void parseStatusReadsQueueCount() {
        QueueStatusResponse result = parser.parseStatus("{\"count\":3}");

        assertEquals(3, result.count());
    }

    @Test
    void parseNextReadsCommitMessage() {
        NextCommitResponse result = parser.parseNext(
            "{\"staged\":true,\"slice\":\"Commits\",\"message\":\"fix(Commits): add json\",\"remainingCount\":2}"
        );

        assertTrue(result.staged());
        assertFalse(result.empty());
        assertEquals("Commits", result.slice());
        assertEquals("fix(Commits): add json", result.message());
        assertEquals(2, result.remainingCount());
    }

    @Test
    void parseNextReadsEmptyQueueResponse() {
        NextCommitResponse result = parser.parseNext(
            "{\"staged\":false,\"empty\":true,\"message\":null,\"remainingCount\":0}"
        );

        assertFalse(result.staged());
        assertTrue(result.empty());
        assertNull(result.message());
        assertEquals(0, result.remainingCount());
    }

    @Test
    void parseStatusRejectsMissingCount() {
        assertThrows(CliExecutionException.class, () -> parser.parseStatus("{\"entries\":[]}"));
    }

    @Test
    void parseErrorPrefersJsonError() {
        String result = parser.parseError("{\"error\":\"Queue is blocked.\"}", "stderr");

        assertEquals("Queue is blocked.", result);
    }
}
