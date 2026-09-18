import type { GitLogCommit, GitLogGraphLink, GitLogRef, GitLogRow } from '../models/GitChanges';

export const GIT_LOG_LANE_PALETTE = 8;
export const GIT_LOG_LANE_WIDTH = 14;
export const GIT_LOG_ROW_HEIGHT = 28;
export const GIT_LOG_NODE_RADIUS = 3.5;

export function classifyGitLogRef(name: string): GitLogRef {
  if (name === 'HEAD') return { name, kind: 'head' };
  if (name.includes('/')) return { name, kind: 'remote' };
  return { name, kind: 'local' };
}

export function gitLogLaneColorKey(lane: number): `gitLane${number}` {
  return `gitLane${((lane % GIT_LOG_LANE_PALETTE) + GIT_LOG_LANE_PALETTE) % GIT_LOG_LANE_PALETTE}`;
}

export function gitLogLaneX(lane: number, laneWidth = GIT_LOG_LANE_WIDTH): number {
  return lane * laneWidth + laneWidth / 2;
}

export function gitLogGraphWidth(laneCount: number, laneWidth = GIT_LOG_LANE_WIDTH): number {
  return Math.max(1, laneCount) * laneWidth;
}

export function layoutGitLog(commits: GitLogCommit[] | null | undefined): GitLogRow[] {
  const list = commits ?? [];
  const occupied: Array<string | null> = [];
  const rows: GitLogRow[] = [];

  for (const commit of list) {
    const parents = commit.parents ?? [];
    const refs = (commit.refs ?? []).map(classifyGitLogRef);
    const incomingLanes = occupied.flatMap((id, index) => (id == null ? [] : [index]));
    let lane = occupied.indexOf(commit.id);
    if (lane < 0) {
      lane = occupied.findIndex(id => id == null);
      if (lane < 0) {
        lane = occupied.length;
        occupied.push(commit.id);
      } else {
        occupied[lane] = commit.id;
      }
    }

    const passingLanes = occupied.flatMap((id, index) => (id == null || index === lane ? [] : [index]));
    const parentLanes: number[] = [];
    const outgoing: GitLogGraphLink[] = passingLanes.map(index => ({
      fromLane: index,
      toLane: index,
      colorLane: index,
    }));

    if (parents.length === 0) {
      occupied[lane] = null;
    } else {
      parents.forEach((parent, index) => {
        let parentLane = occupied.indexOf(parent);
        if (parentLane < 0) {
          if (index === 0) {
            parentLane = lane;
            occupied[lane] = parent;
          } else {
            parentLane = occupied.findIndex((id, slot) => id == null && slot !== lane);
            if (parentLane < 0) {
              parentLane = occupied.length;
              occupied.push(parent);
            } else {
              occupied[parentLane] = parent;
            }
          }
        } else if (index === 0 && parentLane !== lane) {
          occupied[lane] = null;
        }
        parentLanes.push(parentLane);
        outgoing.push({
          fromLane: lane,
          toLane: parentLane,
          colorLane: parentLane === lane ? lane : parentLane,
        });
      });
    }

    while (occupied.length > 0 && occupied[occupied.length - 1] == null) occupied.pop();

    rows.push({
      commit,
      lane,
      parentLanes,
      incomingLanes,
      outgoing,
      laneCount: Math.max(occupied.length, lane + 1, ...parentLanes.map(value => value + 1), 1),
      checkoutName: refs.find(ref => ref.kind === 'local')?.name
        ?? refs.find(ref => ref.kind === 'remote')?.name
        ?? null,
      refs,
    });
  }

  const maxLanes = Math.max(1, ...rows.map(row => row.laneCount));
  return rows.map(row => ({ ...row, laneCount: maxLanes }));
}

export type GitLogGraphEdge = {
  d: string;
  colorLane: number;
};

export function gitLogGraphEdges(
  row: Pick<GitLogRow, 'incomingLanes' | 'outgoing'>,
  laneWidth = GIT_LOG_LANE_WIDTH,
  height = GIT_LOG_ROW_HEIGHT,
): GitLogGraphEdge[] {
  const mid = height / 2;
  const edges: GitLogGraphEdge[] = [];
  for (const lane of row.incomingLanes) {
    const x = gitLogLaneX(lane, laneWidth);
    edges.push({ d: `M ${x} 0 L ${x} ${mid}`, colorLane: lane });
  }
  for (const link of row.outgoing) {
    const x1 = gitLogLaneX(link.fromLane, laneWidth);
    const x2 = gitLogLaneX(link.toLane, laneWidth);
    if (link.fromLane === link.toLane) {
      edges.push({ d: `M ${x1} ${mid} L ${x2} ${height}`, colorLane: link.colorLane });
      continue;
    }
    const cy = (mid + height) / 2;
    edges.push({ d: `M ${x1} ${mid} C ${x1} ${cy}, ${x2} ${cy}, ${x2} ${height}`, colorLane: link.colorLane });
  }
  return edges;
}

export function formatGitLogTime(iso: string, nowMs = Date.now()): string {
  const then = new Date(iso);
  if (Number.isNaN(then.getTime())) return iso;
  const now = new Date(nowMs);
  const minutes = Math.round((now.getTime() - then.getTime()) / 60000);
  if (minutes >= 0 && minutes < 60) {
    if (minutes < 1) return 'just now';
    return minutes === 1 ? '1 minute ago' : `${minutes} minutes ago`;
  }
  const time = `${pad(then.getHours())}:${pad(then.getMinutes())}`;
  if (then.toDateString() === now.toDateString()) return `Today ${time}`;
  const yesterday = new Date(now);
  yesterday.setDate(now.getDate() - 1);
  if (then.toDateString() === yesterday.toDateString()) return `Yesterday ${time}`;
  return `${pad(then.getDate())}.${pad(then.getMonth() + 1)}.${String(then.getFullYear()).slice(2)} ${time}`;
}

function pad(value: number): string {
  return String(value).padStart(2, '0');
}
