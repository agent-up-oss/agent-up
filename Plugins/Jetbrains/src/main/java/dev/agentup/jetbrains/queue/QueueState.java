package dev.agentup.jetbrains.queue;

import java.util.List;

public final class QueueState {
    public enum Kind {
        LOADING,
        OFFLINE,
        AVAILABLE,
        FAILED
    }

    private final Kind kind;
    private final int count;
    private final List<String> messages;
    private final String message;
    private final String executable;
    private final String operationKind;
    private final boolean operationBlocking;

    private QueueState(Kind kind, int count, List<String> messages, String message, String executable, String operationKind, boolean operationBlocking) {
        this.kind = kind;
        this.count = count;
        this.messages = messages;
        this.message = message;
        this.executable = executable;
        this.operationKind = operationKind;
        this.operationBlocking = operationBlocking;
    }

    public static QueueState loading() {
        return new QueueState(Kind.LOADING, 0, List.of(), null, null, null, false);
    }

    public static QueueState offline(String executable) {
        return new QueueState(Kind.OFFLINE, 0, List.of(), null, executable, null, false);
    }

    public static QueueState available(int count, List<String> messages, String operationKind, boolean operationBlocking) {
        return new QueueState(Kind.AVAILABLE, count, List.copyOf(messages), null, null, operationKind, operationBlocking);
    }

    public static QueueState failed(String message) {
        return new QueueState(Kind.FAILED, 0, List.of(), message, null, null, false);
    }

    public Kind getKind() {
        return kind;
    }

    public int getCount() {
        return count;
    }

    public List<String> getMessages() {
        return messages;
    }

    public String getMessage() {
        return message;
    }

    public String getExecutable() {
        return executable;
    }

    public String getOperationKind() {
        return operationKind;
    }

    public boolean isOperationBlocking() {
        return operationBlocking;
    }
}
