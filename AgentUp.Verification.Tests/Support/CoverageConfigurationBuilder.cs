using AgentUp.Verification.Features.Coverage.Models;

namespace AgentUp.Verification.Tests.Support;

internal sealed class CoverageConfigurationBuilder
{
    private double _minimum = 90d;
    private string _reportDirectory = "artifacts/coverage";
    private readonly List<string> _include = [];
    private readonly List<string> _exclude = [];

    public CoverageConfigurationBuilder WithMinimum(double minimum)
    {
        _minimum = minimum;
        return this;
    }

    public CoverageConfigurationBuilder WithReportDirectory(string reportDirectory)
    {
        _reportDirectory = reportDirectory;
        return this;
    }

    public CoverageConfigurationBuilder WithInclude(params string[] globs)
    {
        _include.AddRange(globs);
        return this;
    }

    public CoverageConfigurationBuilder WithExclude(params string[] globs)
    {
        _exclude.AddRange(globs);
        return this;
    }

    public CoverageConfigurationBuilder WithoutInclude()
    {
        _include.Clear();
        return this;
    }

    public CoverageConfiguration Build()
        => new(_minimum, _reportDirectory, _include, _exclude);
}
