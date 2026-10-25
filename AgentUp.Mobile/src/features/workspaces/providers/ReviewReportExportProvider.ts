export type ReviewReportSaver = {
  save(html: string, fileName: string): Promise<void>;
};

export function reviewReportFileName(displayName: string): string {
  const slug = displayName.replace(/[^A-Za-z0-9]+/g, '-').replace(/^-|-$/g, '');
  return slug ? `agent-up-report-${slug}.html` : 'agent-up-report.html';
}

export async function saveReviewReport(
  html: string,
  fileName: string,
  saver: ReviewReportSaver = defaultReviewReportSaver,
): Promise<void> {
  await saver.save(html, fileName);
}

const defaultReviewReportSaver: ReviewReportSaver = {
  async save(html, fileName) {
    if (typeof document !== 'undefined') {
      const blob = new Blob([html], { type: 'text/html;charset=utf-8' });
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
    await Share.share({ title: fileName, message: html });
  },
};
