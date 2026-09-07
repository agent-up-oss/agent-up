namespace AgentUp.Desktop.Features.Database.ViewModels;

public sealed class DatabaseColumnViewModel
{
    public DatabaseColumnViewModel(string name, double width)
    {
        Name = name;
        Width = width;
    }

    public string Name { get; }
    public double Width { get; }
}
