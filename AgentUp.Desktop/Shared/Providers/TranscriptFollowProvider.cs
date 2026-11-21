namespace AgentUp.Desktop.Shared.Providers;

/// <summary>
/// Whether a transcript should keep new output in view. It follows only while the reader is at
/// the end; once they scroll up to read, new output arrives below without moving them.
/// </summary>
public static class TranscriptFollowProvider
{
    /// <summary>Within this many pixels of the end the reader counts as following along.</summary>
    public const double Threshold = 80;

    public static bool IsFollowing(double offset, double viewport, double extent) =>
        extent - (offset + viewport) <= Threshold;
}
