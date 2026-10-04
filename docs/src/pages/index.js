import React, { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import Layout from '@theme/Layout';
import Link from '@docusaurus/Link';
import catalog from '@agent-up/design-system/catalog';
import screenshots from '@agent-up/design-system/screenshots';
import voice from '@agent-up/design-system/brand/voice.json';
import styles from './index.module.css';

const catalogById = Object.fromEntries(
  catalog.surfaces.flatMap(surface => surface.components.map(component => [component.id, component.html])),
);

function catalogHtml(id, replacements = {}) {
  let html = catalogById[id];
  if (!html) {
    throw new Error(`Missing catalog component: ${id}`);
  }
  for (const [from, to] of Object.entries(replacements)) {
    html = html.replaceAll(from, to);
  }
  return html;
}

function capabilityChoice(name, ready) {
  const action = ready
    ? '<button class="au-button au-button--secondary au-button--compact" type="button">Disable</button>'
    : '<button class="au-button au-button--compact" type="button">Enable</button>';
  return `<div class="au-cluster" style="justify-content: space-between;"><strong>${name}</strong>${action}</div>`;
}

const featurePreviews = {
  workspaces: `<div class="au-stack">${catalogHtml('workspace-head')}${catalogHtml('workspace')}</div>`,
  applications: `<div class="au-app-list">
    <div class="au-workspace">
      <span class="au-status-dot au-status-dot--healthy"></span>
      <span>
        <strong class="au-workspace-name">Storefront</strong>
        <small class="au-workspace-branch">Running</small>
      </span>
    </div>
    <div class="au-workspace">
      <span class="au-status-dot au-status-dot--healthy"></span>
      <span>
        <strong class="au-workspace-name">Orders API</strong>
        <small class="au-workspace-branch">Running</small>
      </span>
    </div>
  </div>`,
  agents: `<div class="au-stack au-agent-picker">
    <button class="au-choice au-choice--compact" type="button"><span class="au-workspace-name">Codex</span><span class="au-accent">Available</span></button>
    <button class="au-choice au-choice--compact" type="button"><span class="au-workspace-name">Cursor</span><span class="au-accent">Available</span></button>
    <button class="au-choice au-choice--compact" type="button"><span class="au-workspace-name">Claude</span><span class="au-accent">Available</span></button>
  </div>`,
  git: `<div class="au-stack">
    <button class="au-button au-button--secondary au-button--compact au-git-branch" type="button">main</button>
    <div class="au-cluster au-git-actions">
      <button class="au-button au-button--secondary au-button--compact" type="button">Fetch</button>
      <button class="au-button au-button--secondary au-button--compact" type="button">Pull</button>
      <button class="au-button au-button--compact" type="button">Push</button>
    </div>
    <p class="au-muted">0 of 2 file(s) selected</p>
  </div>`,
  history: `<div class="au-stack">
    <span class="au-git-log-subject">feat: weekly promo</span>
    ${catalogHtml('git-log-mobile')}
  </div>`,
  diagnostics: `<div class="au-stack">${catalogHtml('audit-table')}${catalogHtml('page-jump')}</div>`,
  validation: (() => {
    const html = catalogHtml('validation-stage');
    const lastStage = html.lastIndexOf('<div class="au-validation-stage">');
    return lastStage === -1 ? html : `${html.slice(0, lastStage)}</div>`;
  })(),
  capabilities: `<div class="au-stack">${capabilityChoice('Claude', false)}${capabilityChoice('Codex', true)}${capabilityChoice('Cursor', true)}</div>`,
  'sign-in': `<div class="au-stack">
    <div class="au-sign-in-list">
      <button class="au-choice au-choice--compact au-choice--selected" type="button" title="Demo"><span class="au-choice-label">Demo</span></button>
      <button class="au-choice au-choice--compact" type="button" title="http://localhost:5001"><span class="au-choice-label">http://localhost:5001</span></button>
    </div>
    <button class="au-button au-button--compact" type="button">Connect</button>
  </div>`,
};

const featureIds = [
  'workspaces',
  'applications',
  'agents',
  'git',
  'history',
  'diagnostics',
  'validation',
  'capabilities',
  'sign-in',
];

const featureTitles = {
  capabilities: 'Capability plugins',
  'sign-in': 'Multi-server support',
};

const featureLines = {
  workspaces: 'Isolated space for your branch',
  applications: 'Running apps as managed tabs',
  git: 'Review the working-tree changes',
  history: 'Commit log, graph, and detail',
  agents: 'Each workspace has multiple agents',
  diagnostics: 'Stream and filter app logs',
  validation: 'Recorded flows with nested checks',
  capabilities: 'Enable Server-owned capability modules',
  'sign-in': 'Save and switch among Servers',
};

function sceneFile(sceneId) {
  return sceneId ? `/${sceneId}.png` : null;
}

function featureFromScreen(screen) {
  const desktop = sceneFile(screen.desktopId);
  const mobile = sceneFile(screen.mobileId);
  return {
    id: screen.id,
    title: featureTitles[screen.id] ?? screen.title,
    line: featureLines[screen.id] ?? '',
    desktop,
    mobile,
    preview: featurePreviews[screen.id],
    platforms: [desktop && 'Desktop', mobile && 'Mobile'].filter(Boolean).join(' and '),
  };
}

function FeatureCard({ feature, onOpen }) {
  const onKeyDown = event => {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      onOpen();
    }
  };

  return <div
    className={`au-feature-card au-feature-card--${feature.id}`}
    role="button"
    tabIndex={0}
    onClick={onOpen}
    onKeyDown={onKeyDown}
  >
    <div className="au-feature-card__preview" aria-hidden="true">
      <div className="au-feature-card__preview-inner" dangerouslySetInnerHTML={{ __html: feature.preview }} />
    </div>
    <div className="au-feature-card__body">
      <h3 className="au-heading">{feature.title}</h3>
      <p className="au-muted">{feature.line}</p>
    </div>
  </div>;
}

function Shot({ src, width, height, alt, label }) {
  return <figure className="au-feature-modal__shot" style={{ '--au-preview-aspect': width / height }}>
    <img src={src} width={width} height={height} alt={alt} />
    {label ? <figcaption className="au-field-label">{label}</figcaption> : null}
  </figure>;
}

function ShotPair({ desktop, mobile, captions }) {
  return <div className="au-feature-modal__pair">
    {desktop ? <Shot src={desktop} width={1440} height={900} alt="Desktop" label={captions ? 'Desktop' : undefined} /> : null}
    {mobile ? <Shot src={mobile} width={390} height={844} alt="Mobile" label={captions ? 'Mobile' : undefined} /> : null}
  </div>;
}

function FeatureModal({ feature, onClose }) {
  const panelRef = useRef(null);

  useLayoutEffect(() => {
    const html = document.documentElement;
    const previousOverflow = html.style.overflow;
    html.style.overflow = 'hidden';
    panelRef.current?.focus({ preventScroll: true });
    return () => {
      html.style.overflow = previousOverflow;
    };
  }, []);

  useEffect(() => {
    const onKey = event => { if (event.key === 'Escape') onClose(); };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [onClose]);

  return createPortal(<div className="au-scrim au-feature-modal" onClick={onClose}>
    <div
      ref={panelRef}
      className="au-overlay-panel au-feature-modal__panel"
      role="dialog"
      aria-modal="true"
      aria-labelledby={`feature-${feature.id}-title`}
      tabIndex={-1}
      onClick={event => event.stopPropagation()}
    >
      <div className="au-feature-modal__head">
        <div>
          <p className="au-eyebrow">{feature.platforms}</p>
          <h3 className="au-page-title" id={`feature-${feature.id}-title`}>{feature.title}</h3>
          <p className="au-muted">{feature.line}</p>
        </div>
        <button className="au-workspace-add" type="button" onClick={onClose} aria-label="Close">×</button>
      </div>
      <ShotPair desktop={feature.desktop} mobile={feature.mobile} captions />
    </div>
  </div>, document.body);
}

export default function Home() {
  const features = useMemo(
    () => featureIds
      .map(id => screenshots.screens.find(screen => screen.id === id))
      .filter(Boolean)
      .map(featureFromScreen),
    [],
  );
  const [openId, setOpenId] = useState(null);
  const open = features.find(item => item.id === openId) ?? null;
  const closeFeature = useCallback(() => setOpenId(null), []);

  return <Layout title="Agent-Up" description={voice.category}>
    <main className={`au-theme ${styles.page}`}>
      <section className="au-marketing-hero au-marketing-hero--workspace" id="features">
        <div>
          <h1 className="au-display au-marketing-hero__lockup">
            <img src="/img/logo.svg" alt="" />
            <span>Agent-Up</span>
          </h1>
          <p className="au-title">Review <span className="au-marketing-hero__emphasis">all</span> your projects</p>
        </div>
        <div className="au-feature-grid">
          {features.map(feature => (
            <FeatureCard key={feature.id} feature={feature} onOpen={() => setOpenId(feature.id)} />
          ))}
        </div>
      </section>

      {open ? <FeatureModal feature={open} onClose={closeFeature} /> : null}

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
