import React from 'react';
import Layout from '@theme/Layout';
import Link from '@docusaurus/Link';
import styles from './index.module.css';

const problems = [
  {
    label: 'Service sprawl',
    title: 'Constantly starting multiple services',
    headline: 'One workspace command brings the whole stack back.',
    body: 'Agent-Up treats every workspace as a managed runtime. Frontend, API, background workers, and infrastructure are launched and restarted as one coherent environment instead of a checklist of forgotten commands.',
    layout: 'services',
    details: ['Frontend', 'API', 'Worker', 'Database'],
  },
  {
    label: 'Infrastructure collisions',
    title: 'Docker infrastructure collisions',
    headline: 'Parallel branches stop fighting over the same infrastructure.',
    body: 'Each workspace owns its infrastructure lifecycle. Containers, databases, networks, and runtime assumptions stay scoped to the worktree that needs them.',
    layout: 'docker',
    details: ['agent-1', 'agent-2', 'review'],
  },
  {
    label: 'Tab overload',
    title: 'Browser tab explosion',
    headline: 'The browser becomes workspace state, not loose debris.',
    body: 'A workspace has one isolated browser profile and stable application tabs. Restarting apps reloads the active session instead of scattering more tabs across your desktop.',
    layout: 'tabs',
    details: ['Frontend', 'Admin', 'Swagger'],
  },
  {
    label: 'Repeated login',
    title: 'Duplicated authentication',
    headline: 'Authenticate once, then let humans and agents share the session.',
    body: 'Humans and AI agents interact with the same workspace browser state. Cookies, local storage, and application state are preserved for validation instead of recreated in another browser.',
    layout: 'auth',
    details: ['Cookies', 'Local Storage', 'IndexedDB'],
  },
  {
    label: 'State drift',
    title: 'Inconsistent runtime state',
    headline: 'One Server knows what is actually running.',
    body: 'The Server is the source of truth for processes, ports, browser profiles, health, diagnostics, and event history. Every client reads the same state.',
    layout: 'state',
    details: ['Processes', 'Ports', 'Health', 'Events'],
  },
  {
    label: 'Manual recovery',
    title: 'Manual process management',
    headline: 'Stop nursing processes back to life by hand.',
    body: 'Workspace operations become server capabilities. CLI, Desktop, and MCP clients request actions from the same orchestration layer instead of each inventing process control.',
    layout: 'process',
    details: ['Restart', 'Stop', 'Logs', 'Status'],
  },
  {
    label: 'Weak validation',
    title: 'Difficult validation of AI-generated implementations',
    headline: 'Review behavior with browser evidence, not just diffs.',
    body: 'Agents can restart a workspace, inspect the shared browser, interact through MCP, retrieve diagnostics, capture screenshots, and generate Playwright tests from the recorded session.',
    layout: 'validation',
    details: ['Inspect', 'Interact', 'Screenshot', 'Playwright'],
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

function ProblemVisual({problem}) {
  if (problem.layout === 'services') {
    return (
      <div className={styles.serviceVisual}>
        {problem.details.map((item) => (
          <div key={item}>{item}</div>
        ))}
      </div>
    );
  }

  if (problem.layout === 'docker') {
    return (
      <div className={styles.dockerVisual}>
        {problem.details.map((item, index) => (
          <div key={item}>
            <span>{item}</span>
            <strong>{`${5200 + index * 100}-${5299 + index * 100}`}</strong>
          </div>
        ))}
      </div>
    );
  }

  if (problem.layout === 'tabs') {
    return (
      <div className={styles.tabsVisual}>
        <div className={styles.tabStack}>
          <span />
          <span />
          <span />
          <span />
        </div>
        <div className={styles.singleBrowser}>
          {problem.details.map((item) => (
            <span key={item}>{item}</span>
          ))}
        </div>
      </div>
    );
  }

  if (problem.layout === 'auth') {
    return (
      <div className={styles.authVisual}>
        <div>Human</div>
        <strong>Shared browser profile</strong>
        <div>Agent</div>
      </div>
    );
  }

  if (problem.layout === 'state') {
    return (
      <div className={styles.stateVisual}>
        <strong>AgentUp.Server</strong>
        {problem.details.map((item) => (
          <span key={item}>{item}</span>
        ))}
      </div>
    );
  }

  if (problem.layout === 'process') {
    return (
      <div className={styles.processVisual}>
        {problem.details.map((item) => (
          <button type="button" key={item}>{item}</button>
        ))}
      </div>
    );
  }

  return (
    <div className={styles.validationVisual}>
      {problem.details.map((item) => (
        <span key={item}>{item}</span>
      ))}
    </div>
  );
}

function ProblemSection({problem, index}) {
  return (
    <section className={`${styles.problemSection} ${styles[problem.layout]}`}>
      <div>
        <p className={styles.problemIndex}>{String(index + 1).padStart(2, '0')} / {problem.label}</p>
        <h2>{problem.title}</h2>
        <h3>{problem.headline}</h3>
        <p>{problem.body}</p>
      </div>
      <ProblemVisual problem={problem} />
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
              <Link className={styles.secondaryAction} to="/developer-guide/architecture">
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
          <p className={styles.eyebrow}>What Agent-Up solves</p>
          <h2>Parallel AI work turns local development into distributed operations.</h2>
          <p>
            Every agent brings another branch, runtime, browser session, port range, and validation loop.
            Agent-Up manages that surrounding workspace layer so your applications stay unchanged and your review
            process stays focused on what changed.
          </p>
        </section>

        <section className={styles.problemSections}>
          {problems.map((problem, index) => (
            <ProblemSection key={problem.title} problem={problem} index={index} />
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
