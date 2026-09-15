import { bench, run } from 'mitata';
import { groupTranscript, presentSessionUpdate } from '../providers/AgentEventPresentationProvider';
import type { TranscriptItem } from '../models/AgentSession';
import { flattenChangeTree } from '../../git/providers/GitChangeTreeProvider';
import type { GitChangeTree } from '../../git/models/GitChanges';

const transcript: TranscriptItem[] = Array.from({ length: 1_000 }, (_, index) => ({
  id: `event-${index}`,
  role: index % 20 === 0 ? 'user' : index % 5 === 0 ? 'agent' : 'tool',
  text: `Transcript event ${index}`,
  toolCallId: `tool-${index}`,
}));
const tree: GitChangeTree = {
  workspaceId: 'workspace', branch: 'main', fileCount: 1_000,
  root: { name: '', path: '', directories: Array.from({ length: 20 }, (_, directory) => ({
    name: `slice-${directory}`, path: `slice-${directory}`, directories: [],
    files: Array.from({ length: 50 }, (_, file) => ({ name: `file-${file}.ts`, path: `slice-${directory}/file-${file}.ts`, status: 'Modified' })),
  })), files: [] },
};
const plan = { sessionUpdate: 'plan', entries: Array.from({ length: 100 }, (_, index) => ({ content: `Task ${index}`, status: 'in_progress' })) };

bench('group 1,000 agent transcript items for the mobile view', () => groupTranscript(transcript));
bench('present a 100-entry agent plan in the mobile view', () => presentSessionUpdate(plan));
bench('flatten a 1,000-file Git tree for the mobile view', () => flattenChangeTree(tree));

void run();
