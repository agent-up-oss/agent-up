namespace AgentUp.Desktop.Features.Database.ViewModels;

public sealed class DatabaseCellViewModel
{
    public DatabaseCellViewModel(string value, double width)
    {
        Value = value;
        Width = width;
    }

    public string Value { get; }
    public double Width { get; }
}
