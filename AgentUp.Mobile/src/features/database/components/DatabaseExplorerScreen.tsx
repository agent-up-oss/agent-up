import { useCallback, useEffect, useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import { executeQuery, listDatabases, listTables } from '../providers/DatabaseApiProvider';
import { buildColumnWidths, buildDefaultQuery } from '../providers/DatabaseExplorerProvider';

type DatabaseExplorerScreenProps = {
  workspaceId: string;
  applicationName: string;
};

/** Replaces the application webview when the Server declared `database: true`. */
export function DatabaseExplorerScreen({ workspaceId, applicationName }: DatabaseExplorerScreenProps) {
  const { server } = useWorkspaces();
  const [databases, setDatabases] = useState<string[]>([]);
  const [tables, setTables] = useState<string[]>([]);
  const [selectedDatabase, setSelectedDatabase] = useState<string | null>(null);
  const [selectedTable, setSelectedTable] = useState<string | null>(null);
  const [sql, setSql] = useState('');
  const [columns, setColumns] = useState<string[]>([]);
  const [rows, setRows] = useState<string[][]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const runQuery = useCallback(async (database: string, query: string) => {
    if (!server) return;
    setError(null);
    try {
      const result = await executeQuery(server, workspaceId, applicationName, database, query);
      setColumns(result.columns);
      setRows(result.rows);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
      setColumns([]);
      setRows([]);
    }
  }, [applicationName, server, workspaceId]);

  const selectTable = useCallback((database: string, table: string) => {
    const query = buildDefaultQuery(table);
    setSelectedTable(table);
    setSql(query);
    void runQuery(database, query);
  }, [runQuery]);

  const selectDatabase = useCallback(async (database: string) => {
    if (!server) return;
    setSelectedDatabase(database);
    setSelectedTable(null);
    setTables([]);
    setColumns([]);
    setRows([]);
    setSql('');
    try {
      const listed = await listTables(server, workspaceId, applicationName, database);
      setTables(listed.tables);
      const first = listed.tables[0];
      if (first) selectTable(database, first);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    }
  }, [applicationName, selectTable, server, workspaceId]);

  useEffect(() => {
    let active = true;
    if (!server) {
      setError('No Server connection is available.');
      setLoading(false);
      return () => { active = false; };
    }
    setLoading(true);
    setError(null);
    void listDatabases(server, workspaceId, applicationName)
      .then(async catalog => {
        if (!active) return;
        setDatabases(catalog.databases);
        if (catalog.databases.length === 0) {
          setError('No databases found.');
          return;
        }
        await selectDatabase(catalog.databases[0]);
      })
      .catch(reason => {
        if (!active) return;
        setError(reason instanceof Error ? reason.message : String(reason));
      })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [applicationName, selectDatabase, server, workspaceId]);

  const widths = buildColumnWidths(columns, rows);

  return (
    <View testID="database-explorer" style={styles.screen}>
      <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
        {loading && <Text style={styles.muted}>Loading databases…</Text>}
        {!!error && <Text accessibilityRole="alert" style={styles.error}>{error}</Text>}

        <Text style={styles.sectionTitle}>Databases</Text>
        {databases.map(name => (
          <Pressable
            key={name}
            accessibilityRole="button"
            accessibilityLabel={`Select database ${name}`}
            accessibilityState={{ selected: selectedDatabase === name }}
            onPress={() => { void selectDatabase(name); }}
            style={[styles.listItem, selectedDatabase === name && styles.listItemSelected]}>
            <Text style={selectedDatabase === name ? styles.selectedLabel : styles.itemLabel}>{name}</Text>
          </Pressable>
        ))}

        <Text style={styles.sectionTitle}>Tables</Text>
        {tables.map(name => (
          <Pressable
            key={name}
            accessibilityRole="button"
            accessibilityLabel={`Select table ${name}`}
            accessibilityState={{ selected: selectedTable === name }}
            onPress={() => { if (selectedDatabase) selectTable(selectedDatabase, name); }}
            style={[styles.listItem, selectedTable === name && styles.listItemSelected]}>
            <Text style={selectedTable === name ? styles.selectedLabel : styles.itemLabel}>{name}</Text>
          </Pressable>
        ))}

        <Text style={styles.sectionTitle}>Query</Text>
        <TextInput
          accessibilityLabel="SQL query"
          multiline
          value={sql}
          onChangeText={setSql}
          style={styles.editor}
        />
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Run"
          disabled={!selectedDatabase || !sql.trim()}
          onPress={() => { if (selectedDatabase) void runQuery(selectedDatabase, sql); }}
          style={styles.run}>
          <Text style={styles.runLabel}>Run</Text>
        </Pressable>

        {columns.length > 0 && (
          <>
            <Text style={styles.sectionTitle}>Results</Text>
            <ScrollView horizontal contentContainerStyle={styles.resultsScroll}>
              <View style={styles.results}>
                <View style={styles.row}>
                  {columns.map((column, index) => (
                    <View key={column} style={[styles.headerCell, { minWidth: widths[index], flexGrow: 1, flexBasis: widths[index] }]}>
                      <Text numberOfLines={1} style={styles.headerText}>{column}</Text>
                    </View>
                  ))}
                </View>
                {rows.map((row, index) => (
                  <View key={index} style={styles.row}>
                    {row.map((cell, cellIndex) => (
                      <View
                        key={`${index}-${cellIndex}`}
                        style={[styles.dataCell, { minWidth: widths[cellIndex], flexGrow: 1, flexBasis: widths[cellIndex] }]}>
                        <Text numberOfLines={1} style={styles.dataText}>{cell}</Text>
                      </View>
                    ))}
                  </View>
                ))}
              </View>
            </ScrollView>
          </>
        )}
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: agentUpTheme.colors.canvas },
  content: { padding: agentUpTheme.spacing[4], gap: agentUpTheme.spacing[2], paddingBottom: agentUpTheme.spacing[8] },
  sectionTitle: { ...auText('fieldLabel'), marginTop: agentUpTheme.spacing[3] },
  muted: auText('muted'),
  error: auText('badgeDanger'),
  listItem: auBox('listItem'),
  listItemSelected: auBox('listItemSelected'),
  itemLabel: auText('muted'),
  selectedLabel: auText('workspaceName'),
  editor: auBox('codeEditor'),
  run: { ...auBox('dbRun'), alignSelf: 'flex-start', marginTop: agentUpTheme.spacing[2] },
  runLabel: auText('dbRun'),
  resultsScroll: { flexGrow: 1, minWidth: '100%' },
  results: {
    minWidth: '100%',
    borderLeftWidth: 1,
    borderTopWidth: 1,
    borderColor: agentUpTheme.colors.borderSubtle,
  },
  row: { flexDirection: 'row' },
  headerCell: auBox('dbHeaderCell'),
  headerText: auText('dbHeaderCell'),
  dataCell: auBox('dbDataCell'),
  dataText: { ...auText('dbDataCell'), fontFamily: 'monospace' },
});
