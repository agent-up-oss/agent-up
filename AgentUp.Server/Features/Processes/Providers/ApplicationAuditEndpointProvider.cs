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
        var configured = _configuration["AgentUp:PublicUrl"]
            ?? _configuration["urls"]
            ?? _configuration["ASPNETCORE_URLS"]
            ?? "http://127.0.0.1:5000";
        var baseUrl = configured.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
        return $"{baseUrl.TrimEnd('/')}/api/audit/record";
    }
}
