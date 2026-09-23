using AgentUp.Desktop.Features.FakeServer.DTOs;

namespace AgentUp.Desktop.Tests.Support;

internal sealed class FakeBackendRequestDtoBuilder
{
    private string _method = "GET";
    private string _path = "/";
    private string _query = "";
    private string? _body;

    public FakeBackendRequestDtoBuilder Get(string path)
    {
        _method = "GET";
        _path = path;
        return this;
    }

    public FakeBackendRequestDtoBuilder Post(string path, string? body = null)
    {
        _method = "POST";
        _path = path;
        _body = body;
        return this;
    }

    public FakeBackendRequestDtoBuilder Delete(string path)
    {
        _method = "DELETE";
        _path = path;
        return this;
    }

    public FakeBackendRequestDtoBuilder WithQuery(string query)
    {
        _query = query;
        return this;
    }

    public FakeBackendRequestDto Build() => new(_method, _path, _query, _body);
}
