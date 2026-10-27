import compiled from '../../../../../AgentUp.FakeServer/agent-up.css.json';

const reportExtras = `
.au-report { width: min(100% - 2rem, var(--au-content-width)); margin: var(--au-space-8) auto var(--au-space-16); }
.au-report h2 { margin: var(--au-space-8) 0 var(--au-space-3); font-size: var(--au-font-size-lg); font-weight: var(--au-weight-bold); }
.au-report-shot { display: block; max-width: 100%; margin-top: var(--au-space-3); border-radius: var(--au-radius-lg); border: 1px solid var(--au-color-border-subtle); }
.au-report pre.au-code { white-space: pre-wrap; word-break: break-word; max-height: 24rem; overflow: auto; }
.au-report-row { display: grid; grid-template-columns: 9rem 1fr; gap: var(--au-space-3); padding: var(--au-space-2) 0; border-bottom: 1px solid var(--au-color-border-subtle); }
.au-report-row dt { margin: 0; color: var(--au-color-text-muted); font-size: var(--au-font-size-ui-sm); }
.au-report-row dd { margin: 0; }
`.replace(/^\n/, '');

export function fakeReviewReportStyleTag(): string {
  return `<style>${compiled.css}${reportExtras}</style>`;
}
