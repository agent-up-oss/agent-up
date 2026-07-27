package dev.agentup.jetbrains.queue;

public final class QueueState {
    public enum Kind {
        LOADING,
        OFFLINE,
        AVAILABLE,
        FAILED
    }

    private final Kind kind;
    private final int count;
    private final String message;

    private QueueState(Kind kind, int count, String message) {
        this.kind = kind;
        this.count = count;
        this.message = message;
    }

    public static QueueState loading() {
        return new QueueState(Kind.LOADING, 0, null);
    }

    public static QueueState offline() {
        return new QueueState(Kind.OFFLINE, 0, null);
    }

    public static QueueState available(int count) {
        return new QueueState(Kind.AVAILABLE, count, null);
    }

    public static QueueState failed(String message) {
        return new QueueState(Kind.FAILED, 0, message);
    }

    public Kind getKind() {
        return kind;
    }

    public int getCount() {
        return count;
    }

    public String getMessage() {
        return message;
    }
}
