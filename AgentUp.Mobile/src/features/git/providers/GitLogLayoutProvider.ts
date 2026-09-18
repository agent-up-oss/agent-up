import type { GitLogCommit, GitLogRow } from '../models/GitChanges';

export function layoutGitLog(commits: GitLogCommit[] | null | undefined): GitLogRow[] {
  const list = commits ?? [];
  const lanes = new Map<string, number>();
  let nextLane = 0;
  const rows: GitLogRow[] = [];

  for (const commit of list) {
    const parents = commit.parents ?? [];
    const refs = commit.refs ?? [];
    let lane = lanes.get(commit.id);
    if (lane === undefined) {
      lane = nextLane;
      lanes.set(commit.id, nextLane);
      nextLane += 1;
    }

    const parentLanes: number[] = [];
    parents.forEach((parent, index) => {
      if (!lanes.has(parent)) {
        lanes.set(parent, index === 0 ? lane : nextLane);
        if (index !== 0) nextLane += 1;
      }
      parentLanes.push(lanes.get(parent)!);
    });

    rows.push({
      commit,
      lane,
      parentLanes,
      graph: buildGraph(lane, parentLanes, nextLane),
      checkoutName: refs.find(name => name !== 'HEAD') ?? null,
    });
  }

  return rows;
}

function buildGraph(lane: number, parentLanes: number[], laneCount: number): string {
  const width = Math.max(laneCount, 1);
  const cells = Array.from({ length: width }, () => ' ');
  for (const parent of parentLanes) {
    if (parent >= 0 && parent < width) cells[parent] = '│';
  }
  if (lane >= 0 && lane < width) cells[lane] = '*';
  return cells.join(' ');
}
