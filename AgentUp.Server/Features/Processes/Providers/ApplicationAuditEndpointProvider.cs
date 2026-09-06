namespace AgentUp.Server.Features.Processes.Providers;

public sealed class ApplicationAuditEndpointProvider
{
    private readonly IConfiguration _configuration;

    public ApplicationAuditEndpointProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GetRecordEndpoint()
    {
        var configured = new[]
        {
            _configuration["AgentUp:PublicUrl"],
            _configuration["urls"],
            _configuration["ASPNETCORE_URLS"]
        }.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "http://127.0.0.1:5000";
        var baseUrl = configured
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? "http://127.0.0.1:5000";
        return $"{baseUrl.TrimEnd('/')}/api/audit/record";
    }
}
