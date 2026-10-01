import React from 'react';
import Layout from '@theme/Layout';
import Link from '@docusaurus/Link';
import voice from '@agent-up/design-system/brand/voice.json';
import styles from './index.module.css';

const capabilities = [
  ['Workspaces', 'Keep each checkout, branch, and runtime identity unambiguous.'],
  ['Applications', 'Start local processes and Docker services as one managed environment.'],
  ['Ports', 'Allocate workspace-specific ranges instead of negotiating collisions by hand.'],
  ['Browser', 'Keep independent human and automation sessions bound to the right workspace.'],
  ['Diagnostics', 'Bring logs, health, browser events, and runtime evidence back to one place.'],
  ['Review', 'Inspect the running result and its Git changes without turning Agent-Up into an IDE.'],
];

const shots = [
  ['desktop-sign-in.png', 'Desktop sign-in'],
  ['desktop-workspaces.png', 'Desktop workspaces'],
  ['desktop-applications.png', 'Desktop applications'],
  ['desktop-console.png', 'Desktop console'],
  ['desktop-git.png', 'Desktop Git changes'],
  ['desktop-history.png', 'Desktop Git history'],
  ['desktop-agents.png', 'Desktop Agent'],
  ['desktop-diagnostics.png', 'Desktop diagnostics'],
  ['desktop-metrics.png', 'Desktop metrics'],
  ['desktop-validation.png', 'Desktop validation'],
  ['desktop-database.png', 'Desktop database'],
  ['desktop-capabilities.png', 'Desktop capabilities'],
  ['desktop-file-viewer.png', 'Desktop file viewer'],
  ['mobile-sign-in.png', 'Mobile sign-in'],
  ['mobile-workspaces.png', 'Mobile workspaces'],
  ['mobile-apps.png', 'Mobile apps'],
  ['mobile-git.png', 'Mobile Git'],
  ['mobile-review.png', 'Mobile Git review'],
  ['mobile-history.png', 'Mobile Git history'],
  ['mobile-agents.png', 'Mobile Agents'],
  ['mobile-settings.png', 'Mobile settings'],
  ['mobile-file-viewer.png', 'Mobile file viewer'],
];

function ProductFrame() {
  return <div className="au-product-frame" aria-label="Agent-Up Desktop applications">
    <img src="/screenshot.png" width={1440} height={900} alt="Agent-Up Desktop showing Harbor Shop, Storefront, and the workspace browser surface" />
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
            <Link className="au-button" to="/docs/start/downloads">Download</Link>
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

      <section className="au-section"><div className="au-container">
        <p className="au-eyebrow">Product screenshots</p>
        <h2 className="au-title">Every major view, from the design system.</h2>
        <p className="au-lede">These shots are rendered from catalog classes and Demo Harbor Shop copy, then persisted with <code>au-debug screenshots persist</code>.</p>
        <div className={styles.shots}>{shots.map(([file, title]) => <figure className="au-card" key={file}><img src={`/${file}`} alt={title} /><figcaption className="au-muted">{title}</figcaption></figure>)}</div>
      </div></section>

      <section className="au-section"><div className="au-container au-do-dont">
        <article className="au-card"><p className="au-eyebrow">Agent-Up owns</p><h2 className="au-heading">The development environment around your applications.</h2><p className="au-muted">Workspace identity, managed processes, allocated ports, Docker lifecycle, browser automation, diagnostics, and runtime history.</p></article>
        <article className="au-card"><p className="au-eyebrow">Your tools keep owning</p><h2 className="au-heading">Code, editing, containers, and source history.</h2><p className="au-muted">Your application stays framework-agnostic. Git remains Git. Docker remains Docker. Your coding agent connects through MCP.</p></article>
      </div></section>

      <section className={`au-section ${styles.finalCta}`}><div className="au-container au-stack">
        <p className="au-eyebrow">Start with one repository</p><h2 className="au-title">Make parallel runtime review trustworthy.</h2>
        <div className="au-cluster"><Link className="au-button" to="/docs/start/setup">Set up Agent-Up</Link><Link className="au-button au-button--secondary" to="/developer-guide/repo/architecture">See the architecture</Link></div>
      </div></section>
    </main>
  </Layout>;
}
