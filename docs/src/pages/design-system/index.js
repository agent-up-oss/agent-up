import React, { useEffect, useMemo, useState } from 'react';
import Layout from '@theme/Layout';
import Link from '@docusaurus/Link';
import catalog from '@agent-up/design-system/catalog';
import { agentUpTheme } from '@agent-up/design-system/native';
import voice from '@agent-up/design-system/brand/voice.json';
import agentUpMark from '@agent-up/design-system/brand/mark.svg';
import styles from './index.module.css';

const extra = {
  foundations: {
    tokens: [
      ['Canvas', '--au-color-canvas', agentUpTheme.colors.canvas, 'Window, page, and PWA ground.'],
      ['Surface', '--au-color-surface', agentUpTheme.colors.surface, 'Panels without fake elevation.'],
      ['Subtle border', '--au-color-border-subtle', agentUpTheme.colors.borderSubtle, 'Default structure. Never green by default.'],
      ['Primary text', '--au-color-text-primary', agentUpTheme.colors.textPrimary, 'Titles and important labels.'],
      ['Action', '--au-color-accent', agentUpTheme.colors.accent, 'Primary action, not decoration.'],
      ['Healthy', '--au-color-status-healthy', agentUpTheme.colors.statusHealthy, 'Healthy state with a label.'],
      ['Danger', '--au-color-status-danger', agentUpTheme.colors.statusDanger, 'Failure and destructive action.'],
      ['Focus', '--au-color-focus', agentUpTheme.colors.focus, 'Keyboard focus, distinct from success.'],
    ],
  },
  voice: {
    cards: [
      ['Category', voice.category],
      ['Promise', voice.promise],
      ['Boundary', voice.boundary],
    ],
    lifecycle: Object.entries(voice.lifecycle),
  },
};

function Example({ component }) {
  return <article className={styles.example} id={component.id}>
    <div className={styles.exampleHead}>
      <h3 className="au-heading">{component.title}</h3>
      <p className="au-example__meta">
        <code>.{component.rootClass}</code>
        {component.avalonia ? <> · {component.avalonia}</> : null}
        {component.desktopClass ? <> · Desktop <code>{component.desktopClass}</code></> : null}
      </p>
    </div>
    <div className="au-example__preview" dangerouslySetInnerHTML={{ __html: component.html }} />
    {component.note ? <p className="au-muted">{component.note}</p> : null}
  </article>;
}

export default function DesignSystemPage() {
  const surfaces = catalog.surfaces;
  const ids = useMemo(() => surfaces.map(surface => surface.id), [surfaces]);
  const [active, setActive] = useState(ids[0]);

  useEffect(() => {
    const sync = () => {
      const hash = window.location.hash.replace('#', '');
      if (ids.includes(hash)) setActive(hash);
    };
    sync();
    window.addEventListener('hashchange', sync);
    return () => window.removeEventListener('hashchange', sync);
  }, [ids]);

  const select = id => {
    setActive(id);
    window.history.replaceState(null, '', `#${id}`);
  };

  const surface = surfaces.find(item => item.id === active) ?? surfaces[0];

  return <Layout title="Design System" description="The canonical Agent-Up product, interface, and marketing design system.">
    <main className={`au-theme ${styles.page}`}>
      <header className={styles.hero}>
        <div className="au-container au-marketing-hero">
          <div>
            <p className="au-eyebrow">Agent-Up design system</p>
            <h1 className="au-display">The HTML and CSS are the product contract.</h1>
            <p className="au-lede">Every surface below is a live catalog example. Desktop, Mobile, docs, and marketing apply these classes and compiled bindings. They do not invent a second theme from the palette.</p>
            <div className="au-cluster">
              <a className="au-button" href="#catalog">Open the catalog</a>
              <Link className="au-button au-button--secondary" to="/developer-guide/design-system">Implementation contract</Link>
            </div>
          </div>
          <div className="au-product-frame" aria-label="Agent-Up interface example">
            <div className="au-product-frame__chrome">
              <span>☰ &nbsp;↻</span>
              <span className="au-logo-lockup"><img src={agentUpMark} alt="" />Agent-Up</span>
              <span className="au-product-frame__actions">− □ ×</span>
            </div>
            <div className={styles.demoShell}>
              <aside>
                <strong>Workspaces</strong>
                <div className="au-workspace au-workspace--selected"><span className="au-status-dot au-status-dot--healthy" /><span><b className="au-workspace-name">checkout-fix</b><small className="au-workspace-branch">feat/checkout</small></span></div>
                <div className="au-workspace"><span className="au-status-dot" /><span><b className="au-workspace-name">pricing</b><small className="au-workspace-branch">feat/pricing</small></span></div>
              </aside>
              <div>
                <div className="au-tabs">
                  <button className="au-tab au-tab--selected" aria-selected="true">Storefront</button>
                  <button className="au-tab">API</button>
                  <button className="au-tab">Database</button>
                </div>
                <div className={styles.demoContent}>
                  <span className="au-badge au-badge--healthy"><span className="au-status-dot au-status-dot--healthy" />3000:11200</span>
                  <h3>Each tab is a surface</h3>
                  <p>The catalog is grouped the way Agent-Up is used: chrome, workspaces, applications, browser, console, Git, diagnostics, metrics, validation, auth, Mobile, and marketing.</p>
                </div>
              </div>
            </div>
          </div>
        </div>
      </header>

      <div className={styles.shell} id="catalog">
        <div className={`au-container ${styles.catalog}`}>
          <nav className={styles.surfaceNav} aria-label="Design system surfaces">
            <p className="au-eyebrow">Surfaces</p>
            <div className={styles.surfaceList} role="tablist" aria-orientation="vertical">
              {surfaces.map((item, index) => (
                <button
                  key={item.id}
                  id={`tab-${item.id}`}
                  className={`${styles.surfaceTab}${item.id === active ? ` ${styles.surfaceTabSelected}` : ''}`}
                  role="tab"
                  aria-selected={item.id === active}
                  aria-controls={`panel-${item.id}`}
                  onClick={() => select(item.id)}
                >
                  <span className={styles.surfaceIndex}>{String(index + 1).padStart(2, '0')}</span>
                  <span className={styles.surfaceCopy}>
                    <strong>{item.title}</strong>
                    <small>{item.intro}</small>
                    <em>{item.components.length} {item.components.length === 1 ? 'example' : 'examples'}</em>
                  </span>
                </button>
              ))}
            </div>
          </nav>

          <section
            id={`panel-${surface.id}`}
            className={styles.panel}
            role="tabpanel"
            aria-labelledby={`tab-${surface.id}`}
          >
            <p className="au-eyebrow">{String(ids.indexOf(surface.id) + 1).padStart(2, '0')} · {surface.title}</p>
            <h2 className="au-title">{surface.title}</h2>
            <p className="au-lede">{surface.intro}</p>

            {surface.id === 'foundations' ? <div className="au-grid">{extra.foundations.tokens.map(([name, token, value, description]) => (
              <article className="au-card" key={token}>
                <div className="au-swatch" style={{ background: `var(${token})` }} />
                <h3 className="au-heading">{name}</h3>
                <code>{token}</code>
                <p className="au-mono au-muted">{value}</p>
                <p className="au-muted">{description}</p>
              </article>
            ))}</div> : null}

            {surface.id === 'voice' ? <>
              <div className={styles.voiceGrid}>{extra.voice.cards.map(([label, copy]) => (
                <article className="au-card" key={label}><p className="au-eyebrow">{label}</p><p className={styles.voiceCopy}>{copy}</p></article>
              ))}</div>
              <div className="au-grid">{extra.voice.lifecycle.map(([label, description]) => (
                <article className="au-card" key={label}><span className="au-badge">{label}</span><p className="au-muted">{description}</p></article>
              ))}</div>
            </> : null}

            <div className={styles.examples}>
              {surface.components.map(component => <Example key={component.id} component={component} />)}
            </div>
          </section>
        </div>
      </div>
    </main>
  </Layout>;
}
