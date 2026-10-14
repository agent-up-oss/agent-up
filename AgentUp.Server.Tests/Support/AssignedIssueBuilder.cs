using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Tests.Support;

internal sealed class AssignedIssueBuilder
{
    private string _id = "1";
    private int _number = 1;
    private string _title = "One";
    private string _body = "";
    private DateTimeOffset _created = DateTimeOffset.UnixEpoch;
    private DateTimeOffset _updated = DateTimeOffset.UnixEpoch;
    private string _url = "https://example/1";

    public AssignedIssueBuilder Numbered(int number)
    {
        _id = number.ToString();
        _number = number;
        _title = $"Issue {number}";
        _url = $"https://example/{number}";
        return this;
    }

    public AssignedIssueBuilder Titled(string title)
    {
        _title = title;
        return this;
    }

    public AssignedIssueBuilder WithBody(string body)
    {
        _body = body;
        return this;
    }

    public AssignedIssueBuilder Identified(string id)
    {
        _id = id;
        return this;
    }

    public AssignedIssueBuilder Created(int days)
    {
        _created = DateTimeOffset.UnixEpoch.AddDays(days);
        return this;
    }

    public AssignedIssueBuilder Updated(int days)
    {
        _updated = DateTimeOffset.UnixEpoch.AddDays(days);
        return this;
    }

    public AssignedIssue Build() => new(_id, _number, _title, _body, _created, _updated, _url);
}
