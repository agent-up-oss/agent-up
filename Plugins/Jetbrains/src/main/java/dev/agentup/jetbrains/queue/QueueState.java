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

    private QueueState(Kind kind, int count, List<String> messages, String message) {
        this.kind = kind;
        this.count = count;
        this.messages = messages;
        this.message = message;
    }

    public static QueueState loading() {
        return new QueueState(Kind.LOADING, 0, List.of(), null);
    }

    public static QueueState offline() {
        return new QueueState(Kind.OFFLINE, 0, List.of(), null);
    }

    public static QueueState available(int count, List<String> messages) {
        return new QueueState(Kind.AVAILABLE, count, List.copyOf(messages), null);
    }

    public static QueueState failed(String message) {
        return new QueueState(Kind.FAILED, 0, List.of(), message);
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
}
