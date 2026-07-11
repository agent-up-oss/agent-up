namespace AgentUp.CLI.Models;

public record AgentUpJson(string Name, List<ApplicationDefinition>? Applications = null);
