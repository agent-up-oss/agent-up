import { bench, run } from 'mitata';
import { groupTranscript } from '../../../../../AgentUp.Chat/src/providers/AgentEventPresentationProvider';
import type { TranscriptItem } from '../../../../../AgentUp.Chat/src/models/AgentSession';
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
bench('groupTranscript1000', () => groupTranscript(transcript));
bench('flattenGitTree1000', () => flattenChangeTree(tree));

void run();
