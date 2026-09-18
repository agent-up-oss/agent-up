using AgentUp.Desktop.Features.Git.DTOs;

namespace AgentUp.Desktop.Features.Git.Providers;

public static class GitLogLayoutProvider
{
    public static IReadOnlyList<GitLogRowDto> Layout(IReadOnlyList<GitLogCommitDto>? commits)
    {
        var list = commits ?? [];
        var lanes = new Dictionary<string, int>(StringComparer.Ordinal);
        var nextLane = 0;
        var rows = new List<GitLogRowDto>(list.Count);
        foreach (var commit in list)
        {
            var parents = commit.Parents ?? [];
            var refs = commit.Refs ?? [];
            if (!lanes.TryGetValue(commit.Id, out var lane))
            {
                lane = nextLane;
                lanes[commit.Id] = nextLane++;
            }

            var parentLanes = new List<int>();
            for (var index = 0; index < parents.Count; index++)
            {
                var parent = parents[index];
                if (!lanes.ContainsKey(parent))
                    lanes[parent] = index == 0 ? lane : nextLane++;
                parentLanes.Add(lanes[parent]);
            }

            rows.Add(new GitLogRowDto(
                commit,
                lane,
                parentLanes,
                BuildGraph(lane, parentLanes, nextLane),
                refs.FirstOrDefault(name => !string.Equals(name, "HEAD", StringComparison.Ordinal))));
        }

        return rows;
    }

    private static string BuildGraph(int lane, IReadOnlyList<int> parentLanes, int laneCount)
    {
        var width = Math.Max(laneCount, 1);
        var cells = new char[width];
        Array.Fill(cells, ' ');
        foreach (var parent in parentLanes.Where(parent => parent >= 0 && parent < width))
            cells[parent] = '│';

        if (lane >= 0 && lane < width)
            cells[lane] = '*';

        return string.Join(' ', cells);
    }
}
