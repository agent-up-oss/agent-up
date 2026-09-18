import { createElement } from 'react';
import { Platform, StyleSheet, View } from 'react-native';
import { agentUpTheme, auBox } from '@agent-up/design-system/native';
import type { GitLogRow } from '../models/GitChanges';
import {
  GIT_LOG_LANE_WIDTH,
  GIT_LOG_NODE_RADIUS,
  GIT_LOG_ROW_HEIGHT,
  gitLogGraphEdges,
  gitLogGraphWidth,
  gitLogLaneColorKey,
  gitLogLaneX,
} from '../providers/GitLogLayoutProvider';

export function GitLogGraphColumn({ row }: { row: GitLogRow }) {
  const width = gitLogGraphWidth(row.laneCount);
  const height = GIT_LOG_ROW_HEIGHT;
  const cx = gitLogLaneX(row.lane);
  const cy = height / 2;
  const color = laneColor(row.lane);
  const isHead = row.refs.some(ref => ref.kind === 'head');
  const edges = gitLogGraphEdges(row);

  if (Platform.OS === 'web') {
    return createElement(
      'svg',
      {
        width,
        height,
        viewBox: `0 0 ${width} ${height}`,
        'aria-hidden': true,
        style: { width, height, display: 'block', flexShrink: 0 },
      },
      edges.map((edge, index) => createElement('path', {
        key: `e${index}`,
        d: edge.d,
        fill: 'none',
        stroke: laneColor(edge.colorLane),
        strokeWidth: 2,
        strokeLinecap: 'round',
        strokeLinejoin: 'round',
      })),
      createElement('circle', {
        cx,
        cy,
        r: GIT_LOG_NODE_RADIUS,
        fill: isHead ? agentUpTheme.colors.canvas : color,
        stroke: color,
        strokeWidth: 2,
      }),
    );
  }

  return (
    <View style={[styles.column, { width, height }]}>
      {edges.map((edge, index) =>
        <NativeEdge key={`e${index}`} d={edge.d} color={laneColor(edge.colorLane)} height={height} />)}
      <View
        style={[
          styles.node,
          auBox('gitLogNode'),
          {
            left: cx - GIT_LOG_NODE_RADIUS,
            top: cy - GIT_LOG_NODE_RADIUS,
            backgroundColor: isHead ? agentUpTheme.colors.canvas : color,
            borderColor: color,
            borderWidth: isHead ? 2 : 0,
          },
        ]}
      />
    </View>
  );
}

function NativeEdge({ d, color, height }: { d: string; color: string; height: number }) {
  const cubic = d.match(/^M ([\d.]+) ([\d.]+) C ([\d.]+) ([\d.]+), ([\d.]+) ([\d.]+), ([\d.]+) ([\d.]+)$/);
  if (cubic) {
    const x1 = Number(cubic[1]);
    const x2 = Number(cubic[7]);
    const left = Math.min(x1, x2);
    const width = Math.max(GIT_LOG_LANE_WIDTH, Math.abs(x2 - x1));
    return (
      <View
        pointerEvents="none"
        style={{
          position: 'absolute',
          left,
          top: height / 2,
          width,
          height: height / 2,
          borderColor: color,
          borderRightWidth: x2 >= x1 ? 2 : 0,
          borderLeftWidth: x2 < x1 ? 2 : 0,
          borderBottomWidth: 2,
          borderBottomRightRadius: x2 >= x1 ? width : 0,
          borderBottomLeftRadius: x2 < x1 ? width : 0,
        }}
      />
    );
  }
  const line = d.match(/^M ([\d.]+) ([\d.]+) L ([\d.]+) ([\d.]+)$/);
  if (!line) return null;
  const x = Number(line[1]);
  const y1 = Number(line[2]);
  const y2 = Number(line[4]);
  return (
    <View
      pointerEvents="none"
      style={{
        position: 'absolute',
        left: x - 1,
        top: Math.min(y1, y2),
        width: 2,
        height: Math.abs(y2 - y1),
        backgroundColor: color,
        borderRadius: 1,
      }}
    />
  );
}

function laneColor(lane: number): string {
  return agentUpTheme.colors[gitLogLaneColorKey(lane)] ?? agentUpTheme.colors.textSecondary;
}

const styles = StyleSheet.create({
  column: { position: 'relative', flexShrink: 0, overflow: 'visible' },
  node: { position: 'absolute', width: GIT_LOG_NODE_RADIUS * 2, height: GIT_LOG_NODE_RADIUS * 2 },
});
