import React, { useEffect, useMemo, useState } from 'react';
import Layout from '@theme/Layout';
import Link from '@docusaurus/Link';
import screenshots from '@agent-up/design-system/screenshots';
import voice from '@agent-up/design-system/brand/voice.json';
import styles from './index.module.css';

const featureIds = [
  'workspaces',
  'applications',
  'git',
  'review',
  'history',
  'agents',
  'diagnostics',
  'validation',
  'capabilities',
];

const featureTitles = {
  review: 'Commits',
  capabilities: 'Configuration',
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
    body: screen.intro,
    desktop,
    mobile,
    preview: desktop ?? mobile,
    platforms: [desktop && 'Desktop', mobile && 'Mobile'].filter(Boolean).join(' and '),
  };
}

function ProductFrame() {
  return <div className="au-product-frame" aria-label="Agent-Up Desktop applications">
    <img src="/screenshot.png" width={1440} height={900} alt="Agent-Up Desktop showing Harbor Shop, Storefront, and the workspace browser surface" />
  </div>;
}

function ShotPair({ desktop, mobile }) {
  return <div className="au-feature-modal__pair">
    {desktop ? <figure className="au-feature-modal__shot" style={{ '--au-preview-aspect': 1440 / 900 }}>
      <img src={desktop} width={1440} height={900} alt="Desktop" />
      <figcaption className="au-field-label">Desktop</figcaption>
    </figure> : null}
    {mobile ? <figure className="au-feature-modal__shot" style={{ '--au-preview-aspect': 390 / 844 }}>
      <img src={mobile} width={390} height={844} alt="Mobile" />
      <figcaption className="au-field-label">Mobile</figcaption>
    </figure> : null}
  </div>;
}

function FeatureModal({ feature, onClose }) {
  useEffect(() => {
    const onKey = event => { if (event.key === 'Escape') onClose(); };
    document.addEventListener('keydown', onKey);
    const previous = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => {
      document.removeEventListener('keydown', onKey);
      document.body.style.overflow = previous;
    };
  }, [onClose]);

  return <div className="au-scrim au-feature-modal" onClick={onClose}>
    <div
      className="au-overlay-panel au-feature-modal__panel"
      role="dialog"
      aria-modal="true"
      aria-labelledby={`feature-${feature.id}-title`}
      onClick={event => event.stopPropagation()}
    >
      <div className="au-feature-modal__head">
        <div>
          <p className="au-eyebrow">{feature.platforms}</p>
          <h3 className="au-page-title" id={`feature-${feature.id}-title`}>{feature.title}</h3>
          <p className="au-muted">{feature.body}</p>
        </div>
        <button className="au-workspace-add" type="button" onClick={onClose} aria-label="Close">×</button>
      </div>
      <ShotPair desktop={feature.desktop} mobile={feature.mobile} />
    </div>
  </div>;
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

      <section className="au-section" id="features"><div className="au-container">
        <p className="au-eyebrow">One Server · many clients</p>
        <h2 className="au-title">One source of truth for what is actually running.</h2>
        <p className="au-lede">Desktop, Mobile, CLI, and MCP request actions and render state. Agent-Up Server owns orchestration. Open a slice to see both clients.</p>
        <div className="au-feature-grid">
          {features.map(feature => (
            <button
              key={feature.id}
              className="au-feature-card"
              type="button"
              onClick={() => setOpenId(feature.id)}
            >
              {feature.preview ? <img className="au-feature-card__shot" src={feature.preview} alt="" /> : null}
              <div className="au-feature-card__body">
                <h3 className="au-heading">{feature.title}</h3>
                <p className="au-muted">{feature.body}</p>
                <p className="au-field-label">{feature.platforms}</p>
              </div>
            </button>
          ))}
        </div>
      </div></section>

      {open ? <FeatureModal feature={open} onClose={() => setOpenId(null)} /> : null}

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
