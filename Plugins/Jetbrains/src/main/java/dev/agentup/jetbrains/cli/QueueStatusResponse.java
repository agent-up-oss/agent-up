package dev.agentup.jetbrains.cli;

import java.util.List;

public record QueueStatusResponse(int count, List<String> messages, String operationKind, boolean operationBlocking) {
}
