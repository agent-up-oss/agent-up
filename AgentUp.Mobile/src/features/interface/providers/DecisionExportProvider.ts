/** Where the decision export ends up, which differs between a browser and a phone. */
export type DecisionExportSaver = {
  save(jsonl: string, fileName: string): Promise<void>;
};

/** One file per export, named so several of them sort together in a downloads folder. */
export function decisionExportFileName(now: Date = new Date()): string {
  const day = now.toISOString().slice(0, 10);
  return `agent-up-product-decisions-${day}.jsonl`;
}

/** How many decisions an export carries, which is what the reader is told afterwards. */
export function exportedCount(jsonl: string): number {
  return jsonl.split('\n').filter(line => line.trim().length > 0).length;
}

export async function saveDecisionExport(
  jsonl: string,
  fileName: string,
  saver: DecisionExportSaver = defaultDecisionExportSaver,
): Promise<void> {
  await saver.save(jsonl, fileName);
}

const defaultDecisionExportSaver: DecisionExportSaver = {
  async save(jsonl, fileName) {
    if (typeof document !== 'undefined') {
      const blob = new Blob([jsonl], { type: 'application/x-ndjson;charset=utf-8' });
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = fileName;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
      return;
    }

    const { Share } = await import('react-native');
    await Share.share({ title: fileName, message: jsonl });
  },
};
