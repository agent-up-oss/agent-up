namespace AgentUp.Server.Features.Diagnostics.Providers;

public sealed class ReviewSnapshotCssProvider
{
    public const string ResourceName = "AgentUp.DesignSystem.agent-up.css";

    public string Load()
    {
        var assembly = typeof(ReviewSnapshotCssProvider).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The compiled design-system stylesheet is missing from the Server.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
