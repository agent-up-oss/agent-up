using AgentUp.Desktop.Features.Git.DTOs;

namespace AgentUp.Desktop.Features.Git.Providers;

public static class GitLogLayoutProvider
{
    public const int LanePalette = 8;
    public const double LaneWidth = 14;
    public const double RowHeight = 28;
    public const double NodeRadius = 3.5;

    public static IReadOnlyList<GitLogRowDto> Layout(IReadOnlyList<GitLogCommitDto>? commits)
    {
        var list = commits ?? [];
        var occupied = new List<string?>();
        var rows = new List<GitLogRowDto>(list.Count);
        foreach (var commit in list)
        {
            var parents = commit.Parents ?? [];
            var refs = (commit.Refs ?? []).Select(ClassifyRef).ToList();
            var incomingLanes = occupied
                .Select((id, index) => id is null ? -1 : index)
                .Where(index => index >= 0)
                .ToList();
            var lane = occupied.IndexOf(commit.Id);
            if (lane < 0)
            {
                lane = occupied.FindIndex(id => id is null);
                if (lane < 0)
                {
                    lane = occupied.Count;
                    occupied.Add(commit.Id);
                }
                else
                    occupied[lane] = commit.Id;
            }

            var parentLanes = new List<int>();
            var outgoing = occupied
                .Select((id, index) => id is null || index == lane ? -1 : index)
                .Where(index => index >= 0)
                .Select(index => new GitLogGraphLinkDto(index, index, index))
                .ToList();

            if (parents.Count == 0)
                occupied[lane] = null;
            else
            {
                for (var index = 0; index < parents.Count; index++)
                {
                    var parent = parents[index];
                    var parentLane = occupied.IndexOf(parent);
                    if (parentLane < 0)
                    {
                        if (index == 0)
                        {
                            parentLane = lane;
                            occupied[lane] = parent;
                        }
                        else
                        {
                            parentLane = -1;
                            for (var slot = 0; slot < occupied.Count; slot++)
                            {
                                if (occupied[slot] is null && slot != lane)
                                {
                                    parentLane = slot;
                                    break;
                                }
                            }
                            if (parentLane < 0)
                            {
                                parentLane = occupied.Count;
                                occupied.Add(parent);
                            }
                            else
                                occupied[parentLane] = parent;
                        }
                    }
                    else if (index == 0 && parentLane != lane)
                        occupied[lane] = null;

                    parentLanes.Add(parentLane);
                    outgoing.Add(new GitLogGraphLinkDto(lane, parentLane, parentLane == lane ? lane : parentLane));
                }
            }

            while (occupied.Count > 0 && occupied[^1] is null)
                occupied.RemoveAt(occupied.Count - 1);

            rows.Add(new GitLogRowDto(
                commit,
                lane,
                parentLanes,
                incomingLanes,
                outgoing,
                Math.Max(occupied.Count, Math.Max(lane + 1, parentLanes.Count == 0 ? 1 : parentLanes.Max() + 1)),
                refs.FirstOrDefault(item => item.Kind == "local")?.Name
                    ?? refs.FirstOrDefault(item => item.Kind == "remote")?.Name,
                refs));
        }

        var maxLanes = rows.Count == 0 ? 1 : Math.Max(1, rows.Max(row => row.LaneCount));
        return rows.Select(row => row with { LaneCount = maxLanes }).ToList();
    }

    public static double LaneX(int lane) => lane * LaneWidth + LaneWidth / 2;

    public static double GraphWidth(int laneCount) => Math.Max(1, laneCount) * LaneWidth;

    public static GitLogRefDto ClassifyRef(string name)
    {
        if (string.Equals(name, "HEAD", StringComparison.Ordinal))
            return new GitLogRefDto(name, "head");
        if (name.Contains('/', StringComparison.Ordinal))
            return new GitLogRefDto(name, "remote");
        return new GitLogRefDto(name, "local");
    }

    public static string FormatTime(string iso, DateTimeOffset? now = null)
    {
        if (!DateTimeOffset.TryParse(iso, out var then))
            return iso;
        var clock = now ?? DateTimeOffset.Now;
        var minutes = (int)Math.Round((clock - then).TotalMinutes);
        if (minutes >= 0 && minutes < 60)
            return minutes < 1 ? "just now" : minutes == 1 ? "1 minute ago" : $"{minutes} minutes ago";

        var local = then.ToLocalTime();
        var today = clock.ToLocalTime();
        var time = local.ToString("HH:mm");
        if (local.Date == today.Date)
            return $"Today {time}";
        if (local.Date == today.Date.AddDays(-1))
            return $"Yesterday {time}";
        return $"{local:dd.MM.yy} {time}";
    }
}
