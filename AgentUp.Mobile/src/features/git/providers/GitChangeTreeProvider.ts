import { agentUpTheme } from '@agent-up/design-system/native';
import type { GitChangeDirectory, GitChangeNode, GitChangeTree } from '../models/GitChanges';

// Flattens the Server-owned directory tree into indented rows: directories first, then files,
// mirroring the directory mode of a commit window.
export function flattenChangeTree(tree: GitChangeTree | null): GitChangeNode[] {
  if (!tree) return [];
  const children = flattenDirectory(tree.root, 1, true);
  if (children.length === 0) return [];
  return [
    {
      key: 'root:changes',
      name: 'Changes',
      path: '',
      depth: 0,
      isDirectory: true,
      status: null,
    },
    ...children,
  ];
}

export function filePathsUnder(nodes: GitChangeNode[], node: GitChangeNode): string[] {
  if (!node.isDirectory) return [node.path];
  if (node.path.length === 0) return allFilePaths(nodes);
  const prefix = `${node.path}/`;
  return nodes
    .filter(candidate => !candidate.isDirectory && candidate.path.startsWith(prefix))
    .map(candidate => candidate.path);
}

// A directory is checked exactly when it holds at least one file and every file under it is selected.
export function isDirectorySelected(nodes: GitChangeNode[], node: GitChangeNode, selected: string[]): boolean {
  const paths = filePathsUnder(nodes, node);
  return paths.length > 0 && paths.every(path => selected.includes(path));
}

export function toggleNodeSelection(
  nodes: GitChangeNode[],
  node: GitChangeNode,
  selected: string[],
): string[] {
  const paths = filePathsUnder(nodes, node);
  const shouldSelect = node.isDirectory
    ? !isDirectorySelected(nodes, node, selected)
    : !selected.includes(node.path);

  const remaining = selected.filter(path => !paths.includes(path));
  return shouldSelect ? [...remaining, ...paths] : remaining;
}

export function selectedFilePaths(nodes: GitChangeNode[], selected: string[]): string[] {
  return nodes
    .filter(node => !node.isDirectory && selected.includes(node.path))
    .map(node => node.path);
}

export function allFilePaths(nodes: GitChangeNode[]): string[] {
  return nodes.filter(node => !node.isDirectory).map(node => node.path);
}

export function retainSelectedPaths(nodes: GitChangeNode[], selected: string[]): string[] {
  const files = new Set(allFilePaths(nodes));
  return selected.filter(path => files.has(path));
}

export function canDiscardSelection(selectedCount: number, busy: boolean): boolean {
  return selectedCount > 0 && !busy;
}

// Commit is offered only for a non-empty selection with a non-blank message.
export function canCommitSelection(selectedCount: number, message: string): boolean {
  return selectedCount > 0 && message.trim().length > 0;
}

export function statusGlyph(status: GitChangeNode['status']): string {
  switch (status) {
    case 'Added':
      return '+';
    case 'Untracked':
      return '?';
    case 'Deleted':
      return '−';
    case 'Renamed':
      return '→';
    case 'Conflicted':
      return '!';
    case 'Modified':
      return 'M';
    default:
      return '▸';
  }
}

export function statusColor(status: GitChangeNode['status']): string {
  switch (status) {
    case 'Added':
    case 'Untracked':
      return agentUpTheme.colors.accentSoft;
    case 'Deleted':
      return agentUpTheme.colors.statusDanger;
    case 'Renamed':
      return agentUpTheme.colors.statusInfo;
    case 'Conflicted':
      return agentUpTheme.colors.statusWarning;
    case 'Modified':
      return agentUpTheme.colors.textSecondary;
    default:
      return agentUpTheme.colors.textMuted;
  }
}

function flattenDirectory(directory: GitChangeDirectory, depth: number, isRoot: boolean): GitChangeNode[] {
  const nodes: GitChangeNode[] = [];
  if (!isRoot)
    nodes.push({
      key: `dir:${directory.path}`,
      name: directory.name,
      path: directory.path,
      depth,
      isDirectory: true,
      status: null,
    });

  const childDepth = isRoot ? depth : depth + 1;
  for (const child of directory.directories) nodes.push(...flattenDirectory(child, childDepth, false));
  for (const file of directory.files)
    nodes.push({
      key: `file:${file.path}`,
      name: file.name,
      path: file.path,
      depth: childDepth,
      isDirectory: false,
      status: file.status,
    });

  return nodes;
}
