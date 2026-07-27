package dev.agentup.jetbrains.cli;

import java.util.ArrayList;
import java.util.List;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

public final class CliJsonParser {
    public QueueStatusResponse parseStatus(String stdout) {
        Integer count = intField(stdout, "count");
        if (count == null) {
            throw new CliJsonParseException("Agent-Up returned status JSON without a count field.");
        }

        return new QueueStatusResponse(count, stringFields(stdout, "message"));
    }

    public NextCommitResponse parseNext(String stdout) {
        Boolean staged = boolField(stdout, "staged");
        if (staged == null) {
            throw new CliJsonParseException("Agent-Up returned next JSON without a staged field.");
        }

        int remainingCount = intField(stdout, "remainingCount", 0);
        boolean empty = boolField(stdout, "empty", false);
        String slice = stringField(stdout, "slice");
        String message = stringField(stdout, "message");
        return new NextCommitResponse(staged, empty, slice, message, remainingCount);
    }

    public String parseError(String stdout, String stderr) {
        String jsonError = stringField(stdout, "error");
        if (jsonError != null) {
            return jsonError;
        }

        String trimmedError = stderr.trim();
        return trimmedError.isEmpty() ? "Agent-Up command failed." : trimmedError;
    }

    private static int intField(String json, String name, int defaultValue) {
        Integer value = intField(json, name);
        return value == null ? defaultValue : value;
    }

    private static Integer intField(String json, String name) {
        Matcher matcher = Pattern.compile("\"" + Pattern.quote(name) + "\"\\s*:\\s*(-?\\d+)").matcher(json);
        return matcher.find() ? Integer.parseInt(matcher.group(1)) : null;
    }

    private static boolean boolField(String json, String name, boolean defaultValue) {
        Boolean value = boolField(json, name);
        return value == null ? defaultValue : value;
    }

    private static Boolean boolField(String json, String name) {
        Matcher matcher = Pattern.compile("\"" + Pattern.quote(name) + "\"\\s*:\\s*(true|false)", Pattern.CASE_INSENSITIVE).matcher(json);
        return matcher.find() ? Boolean.parseBoolean(matcher.group(1)) : null;
    }

    private static String stringField(String json, String name) {
        if (Pattern.compile("\"" + Pattern.quote(name) + "\"\\s*:\\s*null").matcher(json).find()) {
            return null;
        }

        Matcher matcher = Pattern.compile("\"" + Pattern.quote(name) + "\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"").matcher(json);
        return matcher.find() ? unescape(matcher.group(1)) : null;
    }

    private static List<String> stringFields(String json, String name) {
        Matcher matcher = Pattern.compile("\"" + Pattern.quote(name) + "\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"").matcher(json);
        List<String> values = new ArrayList<>();
        while (matcher.find()) {
            values.add(unescape(matcher.group(1)));
        }

        return values;
    }

    private static String unescape(String value) {
        StringBuilder result = new StringBuilder(value.length());
        for (int index = 0; index < value.length(); index++) {
            char current = value.charAt(index);
            if (current != '\\' || index == value.length() - 1) {
                result.append(current);
                continue;
            }

            char escaped = value.charAt(++index);
            if (escaped == 'u') {
                result.append(unicodeEscape(value, index));
                index += 4;
                continue;
            }

            result.append(switch (escaped) {
                case '"' -> '"';
                case '\\' -> '\\';
                case '/' -> '/';
                case 'b' -> '\b';
                case 'f' -> '\f';
                case 'n' -> '\n';
                case 'r' -> '\r';
                case 't' -> '\t';
                default -> escaped;
            });
        }

        return result.toString();
    }

    private static char unicodeEscape(String value, int escapeIndex) {
        if (escapeIndex + 4 >= value.length()) {
            throw new CliJsonParseException("Agent-Up returned JSON with an incomplete unicode escape.");
        }

        int codePoint = 0;
        for (int offset = 1; offset <= 4; offset++) {
            int digit = Character.digit(value.charAt(escapeIndex + offset), 16);
            if (digit < 0) {
                throw new CliJsonParseException("Agent-Up returned JSON with an invalid unicode escape.");
            }
            codePoint = (codePoint << 4) + digit;
        }

        return (char)codePoint;
    }
}
