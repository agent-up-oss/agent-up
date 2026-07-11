import React from 'react';
import Layout from '@theme/Layout';
import Link from '@docusaurus/Link';
import styles from './index.module.css';

const problems = [
  {
    label: 'Service sprawl',
    title: 'Constantly starting multiple services',
    body: 'Agent-Up treats every workspace as a managed runtime, so agents and developers can restart, inspect, and recover the right services without rebuilding the day by hand.',
  },
  {
    label: 'Infrastructure collisions',
    title: 'Docker infrastructure collisions',
    body: 'Each workspace owns its infrastructure lifecycle. Parallel branches no longer fight over containers, databases, networks, or assumed runtime state.',
  },
  {
    label: 'Tab overload',
    title: 'Browser tab explosion',
    body: 'A workspace has one isolated browser profile and stable application tabs. Restarting apps reloads the active session instead of creating more browser clutter.',
  },
  {
    label: 'Repeated login',
    title: 'Duplicated authentication',
    body: 'Humans and AI agents share the same workspace browser session, preserving cookies, local storage, and application state for validation.',
  },
  {
    label: 'State drift',
    title: 'Inconsistent runtime state',
    body: 'The Server is the source of truth for processes, ports, browser profiles, health, diagnostics, and event history across every workspace.',
  },
  {
    label: 'Manual recovery',
    title: 'Manual process management',
    body: 'Workspace operations become server capabilities. CLI, Desktop, and MCP clients request actions instead of each inventing their own orchestration.',
  },
  {
    label: 'Weak validation',
    title: 'Difficult validation of AI-generated implementations',
    body: 'Agents can restart a workspace, inspect the shared browser, interact through MCP, retrieve diagnostics, capture screenshots, and generate Playwright tests.',
  },
];

const capabilities = [
  'Server-owned runtime state',
  'Per-workspace port ranges',
  'Isolated browser profiles',
  'MCP-first automation',
  'Event-backed diagnostics',
  'Playwright generation',
];

function ProblemCard({problem}) {
  return (
    <article className={styles.problemCard}>
      <span>{problem.label}</span>
      <h3>{problem.title}</h3>
      <p>{problem.body}</p>
    </article>
  );
}

function BenefitSection({benefit, index}) {
  return (
    <section className={styles.benefitSection}>
      <div className={styles.benefitNumber}>{String(index + 1).padStart(2, '0')}</div>
      <div>
        <h3>{benefit.title}</h3>
        <p>{benefit.body}</p>
      </div>
    </section>
  );
}

export default function Home() {
  return (
    <Layout
      title="Agent-Up"
      description="Workspace management for AI-assisted software development">
      <main className={styles.page}>
        <section className={styles.hero}>
          <div className={styles.heroText}>
            <p className={styles.eyebrow}>AI-assisted development workspaces</p>
            <h1>See what your AI built. Instantly.</h1>
            <p className={styles.lede}>
              Every AI gets its own workspace.
              No tabs. No ports. No setup. Just review.
            </p>
            <div className={styles.actions}>
              <Link className={styles.primaryAction} to="/docs/">
                Read the docs
              </Link>
              <Link className={styles.secondaryAction} to="/docs/architecture">
                See architecture
              </Link>
            </div>
          </div>
          <div className={styles.heroVisual} aria-label="Agent-Up workspace topology">
            <div className={styles.windowChrome}>
              <span />
              <span />
              <span />
            </div>
            <div className={styles.workspaceRail}>
              <strong>Workspaces</strong>
              <div className={styles.agentActive}>agent-1</div>
              <div>agent-2</div>
              <div>review</div>
            </div>
            <div className={styles.runtimePanel}>
              <div className={styles.tabs}>
                <span>Frontend</span>
                <span>API</span>
                <span>Logs</span>
              </div>
              <div className={styles.browserFrame}>
                <div className={styles.browserBar}>Shared browser session</div>
                <div className={styles.portGrid}>
                  <span>5100</span>
                  <span>5101</span>
                  <span>5102</span>
                  <span>MCP</span>
                </div>
              </div>
            </div>
          </div>
        </section>


        <section id="problems" className={styles.problemIntro}>
          <div>
            <p className={styles.eyebrow}>What Agent-Up solves</p>
            <h2>Parallel AI work turns local development into distributed operations.</h2>
          </div>
          <p>
            Agent-Up does not ask applications to change. It manages the surrounding workspace: process lifecycle,
            ports, Docker, browsers, diagnostics, and automation surfaces.
          </p>
        </section>

        <section className={styles.problemGrid}>
          {problems.map((problem) => (
            <ProblemCard key={problem.title} problem={problem} />
          ))}
        </section>

        <section className={styles.operatingModel}>
          <div>
            <p className={styles.eyebrow}>Operating model</p>
            <h2>The Server owns orchestration. Every other surface stays thin.</h2>
            <p>
              Desktop, CLI, and MCP clients all connect to the same Server. That keeps humans and AI agents aligned
              around one authoritative view of every running workspace.
            </p>
          </div>
          <ul>
            {capabilities.map((capability) => (
              <li key={capability}>{capability}</li>
            ))}
          </ul>
        </section>

        <section id="workflow" className={styles.workflow}>
          <p className={styles.eyebrow}>Validation loop</p>
          <h2>From code change to browser evidence without a separate automation harness.</h2>
          <div className={styles.steps}>
            <span>Modify code</span>
            <span>Restart workspace</span>
            <span>Inspect page</span>
            <span>Interact</span>
            <span>Capture diagnostics</span>
            <span>Generate Playwright</span>
          </div>
        </section>
      </main>
    </Layout>
  );
}
