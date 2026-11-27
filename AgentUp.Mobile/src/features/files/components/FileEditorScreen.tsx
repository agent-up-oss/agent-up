import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ActivityIndicator, Platform, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';
import { gitWriteAllowed } from '@/features/entitlements/models/Entitlements';
import { getEntitlements } from '@/features/entitlements/providers/EntitlementsApiProvider';
import { useServers } from '@/features/servers/controllers/ServersContext';
import { isUnauthorized } from '@/features/servers/providers/ServerRequestProvider';
import { ReadingContent } from '@/features/shell/components/ReadingContent';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import { useWorkspaceHost } from '@/features/workspaces/controllers/WorkspaceHostContext';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import type { WorkspaceFileContent } from '../models/WorkspaceFiles';
import { getFile, saveFile } from '../providers/WorkspaceFilesApiProvider';
import {
  caretLabel,
  editorStateLabel,
  fileNameOf,
  fileNotice,
  lineCount,
  lineNumbers,
  statusLabel,
} from '../providers/FileEditorProvider';

const LINE_HEIGHT = 20;
const CHAR_WIDTH = 7.4;
const FILES = { kind: 'files' } as const;
/** The generated native styles leave font families out, so the editor names its monospace face itself. */
const MONO = Platform.select({ ios: 'Menlo', android: 'monospace', default: 'ui-monospace, SFMono-Regular, Menlo, monospace' });

/**
 * One file as its own page: the whole text in a plain editor beside a line gutter, with the caret
 * position, Save and Revert. A save names the version the edit started from, so a change an
 * agent made in the meantime is never overwritten: the Server refuses it and the page offers
 * Reload. Leaving with unsaved changes asks first.
 *
 * `leaveRequest` lets a shell whose back control is not this page's own ask the page to leave,
 * so that back still asks before it throws an edit away.
 */
export function FileEditorScreen({
  workspaceId,
  path,
  leaveRequest = 0,
}: {
  workspaceId: string;
  path: string;
  leaveRequest?: number;
}) {
  const host = useWorkspaceHost(workspaceId);
  const { expireActiveCredential } = useServers();
  const { server } = useWorkspaces();
  const [file, setFile] = useState<WorkspaceFileContent | null>(null);
  const [text, setText] = useState('');
  const [original, setOriginal] = useState('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);
  const [confirmLeave, setConfirmLeave] = useState(false);
  const [canWrite, setCanWrite] = useState(true);
  const [caret, setCaret] = useState(0);
  const [missing, setMissing] = useState(false);
  const ticket = useRef(0);
  const seenLeave = useRef(leaveRequest);

  const hasContent = file?.content != null;
  const dirty = hasContent && text !== original;
  const editable = hasContent && canWrite;

  const leave = useCallback(() => {
    if (dirty) setConfirmLeave(true);
    else host.back(FILES);
  }, [dirty, host]);

  useShellConfig(useMemo(() => ({
    title: fileNameOf(path),
    rightAction: null,
    sidebarContent: null,
    backAction: { label: 'Back', accessibilityLabel: 'Back to files', onPress: leave },
  }), [path, leave]));

  useEffect(() => {
    if (leaveRequest === seenLeave.current) return;
    seenLeave.current = leaveRequest;
    leave();
  }, [leaveRequest, leave]);

  const fail = useCallback((cause: unknown) => {
    if (isUnauthorized(cause)) expireActiveCredential();
    setError(cause instanceof Error ? cause.message : String(cause));
  }, [expireActiveCredential]);

  const load = useCallback(async () => {
    const current = ++ticket.current;
    if (!server) return;
    setLoading(true);
    setError(null);
    setConflict(false);
    setConfirmLeave(false);
    try {
      const read = await getFile(server, workspaceId, path);
      if (current !== ticket.current) return;
      setMissing(read === null);
      setFile(read);
      setText(read?.content ?? '');
      setOriginal(read?.content ?? '');
      setCaret(0);
    } catch (cause) {
      if (current === ticket.current) fail(cause);
    } finally {
      if (current === ticket.current) setLoading(false);
    }
  }, [server, workspaceId, path, fail]);

  useEffect(() => { void load(); }, [load]);

  useEffect(() => {
    if (!server) return;
    let cancelled = false;
    void getEntitlements(server).then(document => {
      if (!cancelled) setCanWrite(gitWriteAllowed(document));
    });
    return () => { cancelled = true; };
  }, [server]);

  const save = useCallback(async () => {
    if (!server || !file || !dirty || saving || conflict) return;
    setSaving(true);
    setError(null);
    try {
      const result = await saveFile(server, workspaceId, path, text, file.version);
      if (result.ok) {
        setFile(result.file);
        setOriginal(text);
      } else {
        setConflict(result.conflict);
        setError(result.error);
      }
    } catch (cause) {
      fail(cause);
    } finally {
      setSaving(false);
    }
  }, [server, file, dirty, saving, conflict, workspaceId, path, text, fail]);

  const lines = useMemo(() => lineCount(text), [text]);
  const gutter = useMemo(() => lineNumbers(lines), [lines]);
  const width = useMemo(() => Math.max(...text.split('\n').map(line => line.length), 20) * CHAR_WIDTH + 32, [text]);
  const notice = missing ? fileNotice(null) : file && !hasContent ? fileNotice(file) : null;

  return (
    <ReadingContent>
    <View style={styles.screen}>
      <View style={[styles.page, auBox('fileEditor')]}>
        <View style={[styles.header, auBox('fileViewerHeader')]}>
          <View style={styles.heading}>
            <Text numberOfLines={1} style={auText('fileViewerPath')}>{path}</Text>
            <Text style={auText('fileViewerStatus')}>{statusLabel(file)}</Text>
          </View>
          {editable &&
            <View style={styles.actions}>
              <Pressable
                testID="file-editor-revert"
                accessibilityRole="button"
                accessibilityLabel="Revert"
                disabled={!dirty}
                onPress={() => { setText(original); setError(null); }}
                style={[styles.action, auBox('button', 'buttonSecondary', 'buttonCompact'), !dirty && styles.disabled]}>
                <Text style={auText('button', 'buttonSecondary', 'buttonCompact')}>Revert</Text>
              </Pressable>
              <Pressable
                testID="file-editor-save"
                accessibilityRole="button"
                accessibilityLabel="Save"
                disabled={!dirty || saving || conflict}
                onPress={() => void save()}
                style={[styles.action, auBox('button', 'buttonCompact'), (!dirty || saving || conflict) && styles.disabled]}>
                <Text style={auText('button', 'buttonCompact')}>Save</Text>
              </Pressable>
            </View>}
        </View>

        {confirmLeave &&
          <View style={[styles.bar, auBox('fileEditorNotice', 'fileEditorNoticeDanger')]}>
            <Text style={[styles.barText, auText('fileEditorNotice', 'fileEditorNoticeDanger')]}>Leave this file and discard the unsaved changes?</Text>
            <Pressable testID="file-editor-keep-editing" accessibilityRole="button" onPress={() => setConfirmLeave(false)}
              style={[styles.action, auBox('button', 'buttonSecondary', 'buttonCompact')]}>
              <Text style={auText('button', 'buttonSecondary', 'buttonCompact')}>Keep editing</Text>
            </Pressable>
            <Pressable testID="file-editor-discard" accessibilityRole="button" onPress={() => host.back(FILES)}
              style={[styles.action, auBox('button', 'buttonDanger', 'buttonCompact')]}>
              <Text style={auText('button', 'buttonCompact')}>Discard</Text>
            </Pressable>
          </View>}
        {conflict &&
          <View style={[styles.bar, auBox('fileEditorNotice', 'fileEditorNoticeDanger')]}>
            <Text style={[styles.barText, auText('fileEditorNotice', 'fileEditorNoticeDanger')]}>{error}</Text>
            <Pressable testID="file-editor-reload" accessibilityRole="button" onPress={() => void load()}
              style={[styles.action, auBox('button', 'buttonSecondary', 'buttonCompact')]}>
              <Text style={auText('button', 'buttonSecondary', 'buttonCompact')}>Reload</Text>
            </Pressable>
          </View>}
        {notice &&
          <View style={auBox('fileEditorNotice')}>
            <Text style={auText('fileEditorNotice')}>{notice}</Text>
          </View>}

        {loading && !hasContent
          ? <ActivityIndicator style={styles.spinner} color={agentUpTheme.colors.accentSoft} />
          : hasContent &&
            <ScrollView style={[styles.body, auBox('fileEditorBody')]} contentContainerStyle={styles.bodyContent}>
              <View style={styles.row}>
                <Text style={[styles.gutter, auText('fileEditorGutter'), auBox('fileEditorGutter')]}>{gutter}</Text>
                <ScrollView horizontal contentContainerStyle={styles.horizontal}>
                  <TextInput
                    testID="file-editor-text"
                    accessibilityLabel={`Contents of ${path}`}
                    value={text}
                    onChangeText={setText}
                    onSelectionChange={event => setCaret(event.nativeEvent.selection.end)}
                    editable={editable}
                    multiline
                    scrollEnabled={false}
                    autoCapitalize="none"
                    autoCorrect={false}
                    spellCheck={false}
                    textAlignVertical="top"
                    style={[
                      auBox('fileEditorInput'),
                      auText('fileEditorInput'),
                      styles.input,
                      { width, height: lines * LINE_HEIGHT + 16 },
                      Platform.OS === 'web' ? webInput : null,
                    ]}
                  />
                </ScrollView>
              </View>
            </ScrollView>}

        <View style={[styles.footer, auBox('fileEditorFooter')]}>
          <Text
            numberOfLines={1}
            style={[styles.state, auText('fileEditorState', error ? 'fileEditorStateError' : dirty || saving ? 'fileEditorStateDirty' : null)]}>
            {editorStateLabel({ dirty, saving, canWrite, hasContent, error: conflict ? null : error })}
          </Text>
          {hasContent && <Text style={auText('fileEditorCaret')}>{caretLabel(text, caret)}</Text>}
        </View>
      </View>
    </View>
    </ReadingContent>
  );
}

/** The browser wraps a textarea by default, which would part the gutter from its lines. */
const webInput = { whiteSpace: 'pre', overflow: 'hidden', resize: 'none', outlineStyle: 'none' } as unknown as object;

const styles = StyleSheet.create({
  screen: { flex: 1, padding: agentUpTheme.spacing[3], backgroundColor: agentUpTheme.colors.canvas },
  page: { flex: 1, minHeight: 0, overflow: 'hidden' },
  header: { flexDirection: 'row', alignItems: 'center', gap: 10 },
  heading: { flex: 1, minWidth: 0, gap: 2 },
  actions: { flexDirection: 'row', gap: 8 },
  action: { alignItems: 'center', justifyContent: 'center', paddingHorizontal: 10, minHeight: 32 },
  disabled: { opacity: 0.45 },
  bar: { flexDirection: 'row', alignItems: 'center', flexWrap: 'wrap', gap: 8 },
  barText: { flex: 1, minWidth: 160 },
  body: { flex: 1, minHeight: 0 },
  bodyContent: { flexGrow: 1 },
  row: { flexDirection: 'row', alignItems: 'flex-start' },
  gutter: { lineHeight: LINE_HEIGHT, textAlign: 'right', paddingTop: 8, fontFamily: MONO },
  horizontal: { flexGrow: 1 },
  input: { lineHeight: LINE_HEIGHT, paddingTop: 8, paddingBottom: 8, fontFamily: MONO },
  state: { flex: 1 },
  spinner: { marginTop: 24 },
  footer: { flexDirection: 'row', alignItems: 'center', gap: 12 },
});
