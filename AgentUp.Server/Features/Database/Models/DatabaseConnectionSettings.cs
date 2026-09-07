namespace AgentUp.Server.Features.Database.Models;

public sealed record DatabaseConnectionSettings(
    string Engine,
    string Host,
    int Port,
    string Username,
    string Password,
    string Database);
