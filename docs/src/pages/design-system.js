import React from 'react';
import Layout from '@theme/Layout';
import Link from '@docusaurus/Link';
import { agentUpTheme } from '@agent-up/design-system/native';
import agentUpMark from '@agent-up/design-system/brand/mark.svg';
import styles from './design-system.module.css';

const colors = [
  ['Canvas', '--au-color-canvas', agentUpTheme.colors.canvas, 'The uninterrupted product and campaign ground.'],
  ['Surface', '--au-color-surface', agentUpTheme.colors.surface, 'Panels and cards without artificial elevation.'],
  ['Subtle border', '--au-color-border-subtle', agentUpTheme.colors.borderSubtle, 'The default structural line. Never green by default.'],
  ['Primary text', '--au-color-text-primary', agentUpTheme.colors.textPrimary, 'Headings and important interface labels.'],
  ['Muted text', '--au-color-text-muted', agentUpTheme.colors.textMuted, 'Supporting copy and inactive metadata.'],
  ['Action', '--au-color-accent', agentUpTheme.colors.accent, 'Primary action—not decoration.'],
  ['Healthy', '--au-color-status-healthy', agentUpTheme.colors.statusHealthy, 'Healthy state with a label or icon.'],
  ['Danger', '--au-color-status-danger', agentUpTheme.colors.statusDanger, 'Failure and destructive action.'],
  ['Focus', '--au-color-focus', agentUpTheme.colors.focus, 'Keyboard focus, distinct from success.'],
];

const voice = [
  ['Category', 'Local runtime control plane for AI-assisted development.'],
  ['Promise', 'Run, inspect, and review parallel development workspaces without process, port, infrastructure, or browser-state collisions.'],
  ['Boundary', 'Agent-Up complements editors, coding agents, Git, Docker, and application frameworks; it does not replace them.'],
];

const lifecycle = [
  ['Available', 'Shipped in the current public development-preview build.'],
  ['Preview', 'Usable, but its contract or experience may change.'],
  ['Experimental', 'Incomplete, opt-in, or not supported on every platform.'],
  ['Planned', 'Product direction only; not available to users.'],
];

function Section({ id, eyebrow, title, intro, children }) {
  return <section id={id} className={`au-section ${styles.section}`}>
    <div className="au-container">
      <p className="au-eyebrow">{eyebrow}</p>
      <h2 className="au-title">{title}</h2>
      {intro && <p className="au-lede">{intro}</p>}
      <div className={styles.sectionBody}>{children}</div>
    </div>
  </section>;
}

function Swatch({ name, token, value, description }) {
  return <article className={`au-card ${styles.swatch}`}>
    <div className={styles.swatchColor} style={{ background: `var(${token})` }} />
    <h3 className="au-heading">{name}</h3>
    <code>{token}</code>
    <span className="au-mono au-muted">{value}</span>
    <p className="au-muted">{description}</p>
  </article>;
}

export default function DesignSystemPage() {
  return <Layout title="Design System" description="The canonical Agent-Up product, interface, and marketing design system.">
    <main className={`au-theme ${styles.page}`}>
      <header className={styles.hero}>
        <div className="au-container au-marketing-hero">
          <div>
            <p className="au-eyebrow">Agent-Up design system · v0.1</p>
            <h1 className="au-display">Quiet structure. Clear runtime state.</h1>
            <p className="au-lede">The single source of truth for Desktop, Mobile, documentation, product screenshots, campaigns, and external Agent-Up marketing.</p>
            <div className="au-cluster">
              <a className="au-button" href="#foundations">Explore the system</a>
              <Link className="au-button au-button--secondary" to="/developer-guide/design-system">Implementation contract</Link>
            </div>
          </div>
          <div className="au-product-frame" aria-label="Agent-Up interface example">
            <div className="au-product-frame__chrome"><span>☰ &nbsp;↻</span><span className="au-logo-lockup"><img src={agentUpMark} alt="" />Agent-Up</span><span className="au-product-frame__actions">− □ ×</span></div>
            <div className={styles.demoShell}>
              <aside>
                <strong>Workspaces</strong>
                <div className={`${styles.workspace} ${styles.workspaceActive}`}><span className="au-status-dot au-status-dot--healthy" /> <span><b>checkout-fix</b><small>feat/checkout</small></span></div>
                <div className={styles.workspace}><span className="au-status-dot" /> <span><b>pricing</b><small>feat/pricing</small></span></div>
              </aside>
              <div className={styles.demoMain}>
                <div className="au-tabs"><button className="au-tab" aria-selected="true">Storefront</button><button className="au-tab">API</button><button className="au-tab">Database</button></div>
                <div className={styles.demoContent}><span className="au-badge au-badge--healthy"><span className="au-status-dot au-status-dot--healthy" />3000:11200</span><h3>Application ready</h3><p>Green identifies the active and healthy. Neutral lines carry the structure.</p></div>
              </div>
            </div>
          </div>
        </div>
      </header>

      <nav className={styles.localNav} aria-label="Design system sections"><div className="au-container au-cluster">
        {['Foundations', 'Components', 'Product', 'Marketing', 'Voice', 'Brand', 'Governance'].map(label => <a key={label} href={`#${label.toLowerCase()}`}>{label}</a>)}
      </div></nav>

      <Section id="foundations" eyebrow="01 · Foundations" title="One visual grammar on every surface" intro="Black is the environment. Off-white carries hierarchy. Neutral gray creates structure. Green appears only when Agent-Up communicates action, selection, progress, or health.">
        <div className="au-grid">{colors.map(color => <Swatch key={color[1]} name={color[0]} token={color[1]} value={color[2]} description={color[3]} />)}</div>
        <div className={styles.typeSpecimen}><p className="au-eyebrow">AI-assisted development workspaces</p><p className={styles.typeHero}>See the runtime.<br/><span className="au-accent">Trust the review.</span></p><p className="au-lede">Use platform-native sans-serif typography in applications and Inter on authored web surfaces. Use monospace only for technical identity: commands, paths, ports, logs, and diffs.</p><code className="au-code">agentup start · localhost:11200 · feat/checkout</code></div>
        <div className="au-do-dont"><div className="au-card"><div className="au-do-dont__label au-do-dont__label--do">✓ Do</div><p>Use neutral boundaries and one meaningful green focal point.</p></div><div className="au-card"><div className="au-do-dont__label au-do-dont__label--dont">× Don’t</div><p>Do not outline every card, diagram, heading, and connector in luminous green.</p></div></div>
      </Section>

      <Section id="components" eyebrow="02 · Components" title="The library is visible, not theoretical" intro="These are live components from @agent-up/design-system. The same semantic roles compile to React Native values and Avalonia resources.">
        <div className={`au-card ${styles.componentLab}`}>
          <div className="au-cluster"><button className="au-button">Start workspace</button><button className="au-button au-button--secondary">View diagnostics</button><button className="au-button au-button--danger">Stop</button></div>
          <label className="au-stack"><strong>Server URL</strong><input className="au-input" placeholder="https://agent-up.example.com" /></label>
          <div className="au-cluster"><span className="au-badge au-badge--healthy"><span className="au-status-dot au-status-dot--healthy" />Server online</span><span className="au-badge"><span className="au-status-dot au-status-dot--warning" />Starting</span><span className="au-badge"><span className="au-status-dot au-status-dot--danger" />Failed</span></div>
          <div className="au-tabs"><button className="au-tab" aria-selected="true">Browser</button><button className="au-tab">Console</button><button className="au-tab">Metrics</button><button className="au-tab">Diagnostics</button></div>
          <aside className="au-callout"><strong>Browser ownership:</strong> Desktop WebViews and Server automation are separate sessions bound to the same workspace runtime.</aside>
        </div>
      </Section>

      <Section id="product" eyebrow="03 · Product surfaces" title="Desktop leads; clients translate" intro="Desktop is the reference rendering. Mobile preserves the hierarchy at touch scale. Docs explain it without becoming a separate visual brand.">
        <div className={styles.surfaceGrid}>
          <article className="au-card"><span className="au-badge">Desktop</span><h3 className="au-heading">Compact and browser-first</h3><ul><li>32px compact chrome and controls</li><li>Neutral separators</li><li>Selected workspace may use a dark green fill</li><li>Native focus remains distinct from health</li></ul></article>
          <article className="au-card"><span className="au-badge">Mobile + PWA</span><h3 className="au-heading">Same hierarchy, touch-safe</h3><ul><li>44–48px controls</li><li>Black through safe areas</li><li>Neutral cards by default</li><li>One selected state per navigation level</li></ul></article>
          <article className="au-card"><span className="au-badge">Docs</span><h3 className="au-heading">Readable before promotional</h3><ul><li>Restrained page width</li><li>Neutral code and table boundaries</li><li>No ambient grid or glow</li><li>Green links and current navigation only</li></ul></article>
        </div>
      </Section>

      <Section id="marketing" eyebrow="04 · Marketing" title="The product is the illustration" intro="Show real state and real workflows. Prefer screenshots and faithful interface fragments over futuristic infrastructure diagrams.">
        <div className="au-proof"><div><p className="au-eyebrow">One promise</p><h3>Parallel runtimes without collisions.</h3><p className="au-muted">A campaign gets one headline and one outcome.</p></div><div><p className="au-eyebrow">One focal point</p><h3>Green identifies the active path.</h3><p className="au-muted">Everything else stays structurally neutral.</p></div><div><p className="au-eyebrow">One truth</p><h3>Label lifecycle visibly.</h3><p className="au-muted">Available, Preview, Experimental, or Planned.</p></div></div>
        <div className="au-do-dont"><div className="au-card"><div className="au-do-dont__label au-do-dont__label--do">✓ Campaign pattern</div><h3 className="au-heading">Run every branch. Review the right one.</h3><p className="au-muted">Use a real product crop, one small status badge, and concrete language.</p></div><div className="au-card"><div className="au-do-dont__label au-do-dont__label--dont">× Retired pattern</div><h3 className="au-heading">AI superpowers. Everywhere. Instantly.</h3><p className="au-muted">Avoid vague superlatives, speculative UI, green glow, and dense circuit diagrams.</p></div></div>
        <div className="au-callout"><strong>Screenshot rule:</strong> capture a real released build, use realistic sanitized data, describe the visible capability in alt text, and record the app version with the source asset.</div>
      </Section>

      <Section id="voice" eyebrow="05 · Voice and content" title="Say exactly what Agent-Up owns" intro="Product writing is direct, technical, and accountable. Lead with what a developer can do; explain Server ownership when it matters.">
        <div className={styles.voiceGrid}>{voice.map(([label, copy]) => <article className="au-card" key={label}><p className="au-eyebrow">{label}</p><p className={styles.voiceCopy}>{copy}</p></article>)}</div>
        <div className="au-grid">{lifecycle.map(([label, description]) => <article className="au-card" key={label}><span className="au-badge">{label}</span><p className="au-muted">{description}</p></article>)}</div>
        <div className="au-do-dont"><div className="au-card"><div className="au-do-dont__label au-do-dont__label--do">✓ Use</div><p>Workspace, application, Agent-Up Server, Desktop WebView, Server automation browser.</p></div><div className="au-card"><div className="au-do-dont__label au-do-dont__label--dont">× Avoid</div><p>Agent Up, shared browser profile, AI magic, orchestration platform, or claims that hide planned status.</p></div></div>
      </Section>

      <Section id="brand" eyebrow="06 · Brand assets" title="One mark, controlled exports" intro="Use the AU arrow as a compact identifier. Keep it small in product chrome and pair it with the exact Agent-Up wordmark in authored marketing.">
        <div className={styles.logoStage}><div className="au-logo-lockup"><img src={agentUpMark} alt="Agent-Up logo"/><span>Agent-Up</span></div><div><p><strong>Safe space:</strong> at least one internal logo stroke on every side.</p><p><strong>Small sizes:</strong> use the mark without glow or surrounding container.</p><p><strong>Naming:</strong> Agent-Up for the brand; <span className="au-mono">agentup</span> for the CLI.</p></div></div>
      </Section>

      <Section id="governance" eyebrow="07 · Governance" title="Hard references, generated bindings" intro="The CSS is canonical. Web consumers import it directly. Native consumers use deterministic generated bindings checked in CI.">
        <pre className="au-code"><code>{`@import '@agent-up/design-system/styles.css';\n@import '@agent-up/design-system/marketing.css';\n\nimport { agentUpTheme } from '@agent-up/design-system/native';\n\n<!-- Avalonia -->\n<ResourceInclude Source=\"/DesignSystem/AgentUpTheme.axaml\" />`}</code></pre>
        <div className={styles.governanceFlow}><span>Canonical HTML + CSS</span><b>→</b><span>Web imports</span><b>+</b><span>React Native binding</span><b>+</b><span>Avalonia resources</span></div>
        <p className="au-muted">Never edit generated files in <span className="au-mono">AgentUp.DesignSystem/dist</span>. Change canonical CSS, rebuild, review every consumer, and run the design-system contract tests.</p>
      </Section>
    </main>
  </Layout>;
}
