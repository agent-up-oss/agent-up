namespace AgentUp.Desktop.Features.Git.ViewModels;

public sealed class GitFileDiffHunkViewModel
{
    public GitFileDiffHunkViewModel(string label, int lineIndex)
    {
        Label = label;
        LineIndex = lineIndex;
    }

    public string Label { get; }
    public int LineIndex { get; }
}
