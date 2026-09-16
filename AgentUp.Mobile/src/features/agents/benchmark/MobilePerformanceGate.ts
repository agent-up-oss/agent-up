import { performance } from 'node:perf_hooks';
import { readFileSync } from 'node:fs';
import { groupTranscript } from '../../../../../AgentUp.Chat/src/providers/AgentEventPresentationProvider';
import type { TranscriptItem } from '../../../../../AgentUp.Chat/src/models/AgentSession';
import { flattenChangeTree } from '../../git/providers/GitChangeTreeProvider';
import type { GitChangeTree } from '../../git/models/GitChanges';

type Limit = { meanMicroseconds: number; maxTimeRatio: number };
const limits = JSON.parse(readFileSync('../benchmarks/baselines/mobile.json', 'utf8')).benchmarks as Record<string, Limit>;
const transcript: TranscriptItem[] = Array.from({ length: 1_000 }, (_, index) => ({ id: `event-${index}`, role: index % 20 === 0 ? 'user' : index % 5 === 0 ? 'agent' : 'tool', text: `Event ${index}`, toolCallId: `tool-${index}` }));
const tree: GitChangeTree = { workspaceId: 'workspace', branch: 'main', fileCount: 1_000, root: { name: '', path: '', directories: Array.from({ length: 20 }, (_, directory) => ({ name: `slice-${directory}`, path: `slice-${directory}`, directories: [], files: Array.from({ length: 50 }, (_, file) => ({ name: `file-${file}.ts`, path: `slice-${directory}/file-${file}.ts`, status: 'Modified' })) })), files: [] } };

function measure(name: string, action: () => unknown): string | null {
  for (let index = 0; index < 100; index++) action();
  const samples: number[] = [];
  for (let sample = 0; sample < 20; sample++) {
    const start = performance.now();
    for (let index = 0; index < 100; index++) action();
    samples.push((performance.now() - start) * 10);
  }
  samples.sort((left, right) => left - right);
  const mean = samples.slice(2, 18).reduce((sum, value) => sum + value, 0) / 16;
  const limit = limits[name].meanMicroseconds * limits[name].maxTimeRatio;
  console.log(`${name}: ${mean.toFixed(1)} us (limit ${limit.toFixed(1)})`);
  return mean > limit ? `${name} mean ${mean.toFixed(1)} us exceeds ${limit.toFixed(1)} us` : null;
}

const failures = [
  measure('groupTranscript1000', () => groupTranscript(transcript)),
  measure('flattenGitTree1000', () => flattenChangeTree(tree)),
].filter((failure): failure is string => failure !== null);
if (failures.length > 0) {
  console.error(`Performance gate failed:\n${failures.map(failure => `- ${failure}`).join('\n')}`);
  process.exitCode = 1;
}
