using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Screenshots.DTOs;
using AgentUp.AUDebug.Features.Screenshots.Interfaces;

namespace AgentUp.AUDebug.Features.Screenshots.Providers;

public sealed class ScreenshotPngComparer : IScreenshotPngComparer
{
    public ScreenshotCompareDto Compare(string expectedPath, string actualPath)
    {
        if (!File.Exists(expectedPath))
            return new ScreenshotCompareDto(false, -1, $"Persisted screenshot '{expectedPath}' is missing. Run au-debug screenshots persist.");
        if (!File.Exists(actualPath))
            return new ScreenshotCompareDto(false, -1, $"Regenerated screenshot '{actualPath}' is missing.");

        var expectedBytes = File.ReadAllBytes(expectedPath);
        var actualBytes = File.ReadAllBytes(actualPath);
        if (expectedBytes.AsSpan().SequenceEqual(actualBytes))
            return new ScreenshotCompareDto(true, 0, "identical bytes");

        var expected = ScreenshotPngCodec.DecodeRgba(expectedPath, out var expectedWidth, out var expectedHeight);
        var actual = ScreenshotPngCodec.DecodeRgba(actualPath, out var actualWidth, out var actualHeight);
        if (expectedWidth != actualWidth || expectedHeight != actualHeight)
        {
            return new ScreenshotCompareDto(
                false,
                -1,
                $"size {actualWidth}x{actualHeight} does not match persisted {expectedWidth}x{expectedHeight}");
        }

        var diffs = 0;
        for (var i = 0; i < expected.Length; i++)
        {
            var delta = expected[i] > actual[i] ? expected[i] - actual[i] : actual[i] - expected[i];
            if (delta > DebugLayout.ScreenshotMaxChannelDelta)
            {
                diffs++;
                i = (i / 4 + 1) * 4 - 1;
            }
        }

        return diffs == 0
            ? new ScreenshotCompareDto(true, 0, "identical pixels")
            : new ScreenshotCompareDto(false, diffs, $"{diffs} pixel(s) differ");
    }
}
