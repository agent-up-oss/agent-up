namespace AgentUp.Server.Features.Forge.Interfaces;

public interface IForgeOAuthExchange
{
    Task<string> ExchangeAsync(string code, string tokenUrl, CancellationToken cancellationToken);
}
