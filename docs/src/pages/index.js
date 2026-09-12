import React from 'react';
import Layout from '@theme/Layout';
import Link from '@docusaurus/Link';
import voice from '@agent-up/design-system/brand/voice.json';
import agentUpMark from '@agent-up/design-system/brand/mark.svg';
import styles from './index.module.css';

const capabilities = [
  ['Workspaces', 'Keep each checkout, branch, and runtime identity unambiguous.'],
  ['Applications', 'Start local processes and Docker services as one managed environment.'],
  ['Ports', 'Allocate workspace-specific ranges instead of negotiating collisions by hand.'],
  ['Browser', 'Keep independent human and automation sessions bound to the right workspace.'],
  ['Diagnostics', 'Bring logs, health, browser events, and runtime evidence back to one place.'],
  ['Review', 'Inspect the running result and its Git changes without turning Agent-Up into an IDE.'],
];

function ProductFrame() {
  return <div className="au-product-frame" aria-label="Agent-Up Desktop workspace example">
    <div className="au-product-frame__chrome">
      <span>☰ &nbsp;↻ &nbsp;<span className="au-badge au-badge--healthy"><span className="au-status-dot au-status-dot--healthy"/>Server online</span></span>
      <span className="au-logo-lockup"><img src={agentUpMark} alt=""/>Agent-Up</span>
      <span className="au-product-frame__actions">− □ ×</span>
    </div>
    <div className={styles.appShell}>
      <aside className={styles.sidebar}>
        <strong>Workspaces</strong>
        <div className={`${styles.workspace} ${styles.workspaceActive}`}><span className="au-status-dot au-status-dot--healthy"/><span><b>checkout-fix</b><small>feat/checkout</small></span></div>
        <div className={styles.workspace}><span className="au-status-dot"/><span><b>pricing</b><small>feat/pricing</small></span></div>
        <div className={styles.workspace}><span className="au-status-dot"/><span><b>main</b><small>main</small></span></div>
      </aside>
      <div className={styles.runtime}>
        <div className="au-tabs"><button className="au-tab" aria-selected="true">Storefront</button><button className="au-tab">API</button><button className="au-tab">Database</button></div>
        <div className={styles.subnav}><span className="au-badge au-badge--healthy"><span className="au-status-dot au-status-dot--healthy"/>3000:11200</span><b>Console</b><b>Metrics</b><b>Diagnostics</b></div>
        <div className={styles.browserBar}>‹ &nbsp; › &nbsp; ↻ <span className="au-mono">http://localhost:11200/</span></div>
        <div className={styles.browser}><p className="au-eyebrow">checkout-fix / storefront</p><h2>Review the runtime that belongs to this change.</h2><p>Applications, ports, diagnostics, and browser automation stay attached to the workspace Agent-Up Server owns.</p></div>
      </div>
    </div>
  </div>;
}

export default function Home() {
  return <Layout title="Agent-Up" description={voice.category}>
    <main className={`au-theme ${styles.page}`}>
      <header className="au-container au-marketing-hero">
        <div>
          <p className="au-eyebrow">Local runtime control plane</p>
          <h1 className="au-display">Run every branch. Review the right one.</h1>
          <p className="au-lede">{voice.promise}</p>
          <div className="au-cluster">
            <Link className="au-button" to="/docs/downloads">Download</Link>
            <Link className="au-button au-button--secondary" to="/docs/">Read the docs</Link>
          </div>
          <p className={styles.preview}>Development preview · APIs and workflows may change.</p>
        </div>
        <ProductFrame />
      </header>

      <section className={`au-section ${styles.statement}`}><div className="au-container">
        <p className="au-eyebrow">Why Agent-Up</p>
        <h2 className="au-title">Source isolation is only half the job.</h2>
        <p className="au-lede">Git worktrees keep changes apart. Agent-Up keeps their running environments identifiable, operable, and reviewable.</p>
      </div></section>

      <section className="au-section"><div className="au-container">
        <p className="au-eyebrow">One Server · many clients</p>
        <h2 className="au-title">One source of truth for what is actually running.</h2>
        <p className="au-lede">Desktop, Mobile, CLI, and MCP request actions and render state. Agent-Up Server owns orchestration.</p>
        <div className={`au-grid ${styles.capabilities}`}>{capabilities.map(([title, body]) => <article className="au-card" key={title}><span className="au-status-dot au-status-dot--healthy"/><h3 className="au-heading">{title}</h3><p className="au-muted">{body}</p></article>)}</div>
      </div></section>

      <section className="au-section"><div className="au-container au-do-dont">
        <article className="au-card"><p className="au-eyebrow">Agent-Up owns</p><h2 className="au-heading">The development environment around your applications.</h2><p className="au-muted">Workspace identity, managed processes, allocated ports, Docker lifecycle, browser automation, diagnostics, and runtime history.</p></article>
        <article className="au-card"><p className="au-eyebrow">Your tools keep owning</p><h2 className="au-heading">Code, editing, containers, and source history.</h2><p className="au-muted">Your application stays framework-agnostic. Git remains Git. Docker remains Docker. Your coding agent connects through MCP.</p></article>
      </div></section>

      <section className={`au-section ${styles.finalCta}`}><div className="au-container au-stack">
        <p className="au-eyebrow">Start with one repository</p><h2 className="au-title">Make parallel runtime review trustworthy.</h2>
        <div className="au-cluster"><Link className="au-button" to="/docs/setup">Set up Agent-Up</Link><Link className="au-button au-button--secondary" to="/developer-guide/architecture">See the architecture</Link></div>
      </div></section>
    </main>
  </Layout>;
}
