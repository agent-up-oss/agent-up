namespace AgentUp.Desktop.Features.Git.ViewModels;

public sealed class GitFileDiffTokenViewModel
{
    public GitFileDiffTokenViewModel(string kind, string text)
    {
        Kind = kind;
        Text = text;
        IsKeyword = kind == "keyword";
        IsType = kind == "type";
        IsString = kind == "string";
        IsComment = kind == "comment";
        IsNumber = kind == "number";
        IsFunction = kind == "function";
        IsProperty = kind == "property";
        IsPunctuation = kind == "punctuation";
        IsOperator = kind == "operator";
    }

    public string Kind { get; }
    public string Text { get; }
    public bool IsKeyword { get; }
    public bool IsType { get; }
    public bool IsString { get; }
    public bool IsComment { get; }
    public bool IsNumber { get; }
    public bool IsFunction { get; }
    public bool IsProperty { get; }
    public bool IsPunctuation { get; }
    public bool IsOperator { get; }
}
