package dev.agentup.jetbrains.cli;

import java.util.ArrayList;
import java.util.List;

public record CliExecutableCommand(String executable, List<String> arguments) {
    public static CliExecutableCommand parse(String value) {
        List<String> parts = split(value == null || value.isBlank() ? "agent-up" : value.trim());
        if (parts.isEmpty()) {
            return new CliExecutableCommand("agent-up", List.of());
        }

        return new CliExecutableCommand(parts.get(0), List.copyOf(parts.subList(1, parts.size())));
    }

    private static List<String> split(String value) {
        List<String> parts = new ArrayList<>();
        StringBuilder current = new StringBuilder();
        Character quote = null;
        boolean escaping = false;

        for (int index = 0; index < value.length(); index++) {
            char character = value.charAt(index);
            if (escaping) {
                current.append(character);
                escaping = false;
                continue;
            }

            if (character == '\\') {
                escaping = true;
                continue;
            }

            if (quote != null) {
                if (character == quote) {
                    quote = null;
                } else {
                    current.append(character);
                }
                continue;
            }

            if (character == '\'' || character == '"') {
                quote = character;
                continue;
            }

            if (Character.isWhitespace(character)) {
                addPart(parts, current);
                continue;
            }

            current.append(character);
        }

        if (escaping) {
            current.append('\\');
        }
        addPart(parts, current);
        return parts;
    }

    private static void addPart(List<String> parts, StringBuilder current) {
        if (!current.isEmpty()) {
            parts.add(current.toString());
            current.setLength(0);
        }
    }
}
