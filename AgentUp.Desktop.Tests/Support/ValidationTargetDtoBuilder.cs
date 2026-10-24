using AgentUp.Desktop.Features.Validation.DTOs;

namespace AgentUp.Desktop.Tests.Support;

/// <summary>
/// Builds a <see cref="ValidationTargetDto"/> so tests name the locator field they mean
/// rather than the six-parameter wire shape.
/// </summary>
internal sealed class ValidationTargetDtoBuilder
{
    private string? _role;
    private string? _name;
    private string? _label;
    private string? _testId;
    private string? _text;
    private string? _selector;

    public ValidationTargetDtoBuilder WithRole(string? role)
    {
        _role = role;
        return this;
    }

    public ValidationTargetDtoBuilder Named(string? name)
    {
        _name = name;
        return this;
    }

    public ValidationTargetDtoBuilder WithLabel(string? label)
    {
        _label = label;
        return this;
    }

    public ValidationTargetDtoBuilder WithTestId(string? testId)
    {
        _testId = testId;
        return this;
    }

    public ValidationTargetDtoBuilder WithText(string? text)
    {
        _text = text;
        return this;
    }

    public ValidationTargetDtoBuilder WithSelector(string? selector)
    {
        _selector = selector;
        return this;
    }

    public ValidationTargetDto Build() =>
        new(_role, _name, _label, _testId, _text, _selector);
}
