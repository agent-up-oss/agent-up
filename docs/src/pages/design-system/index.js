import React, { useEffect, useMemo, useRef, useState } from 'react';
import Layout from '@theme/Layout';
import Link from '@docusaurus/Link';
import catalog from '@agent-up/design-system/catalog';
import screenshots from '@agent-up/design-system/screenshots';
import { playSequence } from '@agent-up/design-system/sequence';
import { agentUpTheme } from '@agent-up/design-system/native';
import voice from '@agent-up/design-system/brand/voice.json';
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
    {(component.stateExamples ?? []).map(example => <SlotExample key={`state:${example.state}`} label={example.state} example={example} />)}
    {(component.propsExamples ?? []).map(example => <SlotExample key={`props:${example.props}`} label={example.props} example={example} />)}
  </article>;
}

/**
 * A documented variation of the component above, generated from that one piece of markup rather
 * than authored as a second component. The declaration a screen would write is shown with it,
 * so a reader can see what produced the example.
 */
function SlotExample({ label, example }) {
  return <div className={`au-example__state ${styles.stateExample}`}>
    <p className="au-field-label">
      {example.title} · <code>{label}</code>
    </p>
    <div className="au-example__preview" dangerouslySetInnerHTML={{ __html: example.html }} />
    <p className="au-muted">{example.note}</p>
  </div>;
}

const playgroundControls = [
  'badge',
  'choice',
  'workspace',
  'app-tab',
  'chat-transcript',
  'git-change-list',
];

const catalogById = Object.fromEntries(
  catalog.surfaces.flatMap(surface => surface.components.map(component => [component.id, component])),
);

const catalogSelection = [
  ['au-tab', 'au-tab--selected'],
  ['au-app-tab', 'au-app-tab--selected'],
  ['au-subtab', 'au-subtab--selected'],
  ['au-choice', 'au-choice--selected'],
  ['au-workspace', 'au-workspace--selected'],
  ['au-git-row', 'au-git-row--selected'],
  ['au-list-item', 'au-list-item--selected'],
  ['au-page-jump', 'au-page-jump--current'],
  ['au-workspace-avatar', 'au-workspace-avatar--selected'],
];

function activateCatalogExample(root, event) {
  const node = event.target;
  if (!(node instanceof Element) || !root.contains(node)) return;
  if (node.closest('input, textarea, select, a')) return;

  const checkbox = node.closest('.au-checkbox');
  if (checkbox && root.contains(checkbox)) {
    event.preventDefault();
    checkbox.classList.toggle('au-checkbox--checked');
    return;
  }

  for (const [base, selected] of catalogSelection) {
    const control = node.closest(`.${base}`);
    if (!control || !root.contains(control) || control.disabled) continue;
    event.preventDefault();
    for (const sibling of root.querySelectorAll(`.${base}`)) {
      sibling.classList.toggle(selected, sibling === control);
      if (sibling.hasAttribute('aria-selected')) sibling.setAttribute('aria-selected', sibling === control ? 'true' : 'false');
      if (sibling.hasAttribute('aria-pressed')) sibling.setAttribute('aria-pressed', sibling === control ? 'true' : 'false');
    }
    return;
  }

  const button = node.closest('button');
  if (button && root.contains(button)) event.preventDefault();
}

function CatalogControl({ component }) {
  const ref = useRef(null);

  useEffect(() => {
    const root = ref.current;
    if (!root) return;
    const onClick = event => activateCatalogExample(root, event);
    const onSubmit = event => event.preventDefault();
    root.addEventListener('click', onClick);
    root.addEventListener('submit', onSubmit);
    return () => {
      root.removeEventListener('click', onClick);
      root.removeEventListener('submit', onSubmit);
    };
  }, [component.html]);

  return <article className="au-card" data-au-component={component.id}>
    <div ref={ref} dangerouslySetInnerHTML={{ __html: component.html }} />
  </article>;
}

function IntroPlayground() {
  const controls = playgroundControls.map(id => catalogById[id]).filter(Boolean);
  return <div className={`au-grid ${styles.playground}`} aria-label="Live catalog components">
    {controls.map(component => <CatalogControl key={component.id} component={component} />)}
  </div>;
}

function useShowcaseDisplayHeight() {
  const [height, setHeight] = useState(448);
  useEffect(() => {
    const media = window.matchMedia('(max-width: 960px)');
    const sync = () => setHeight(media.matches ? 224 : 448);
    sync();
    media.addEventListener('change', sync);
    return () => media.removeEventListener('change', sync);
  }, []);
  return height;
}

function ScenePreview({ scene, label }) {
  const mounted = useRef(null);
  const displayHeight = useShowcaseDisplayHeight();
  const scale = displayHeight / scene.height;

  // A scene that declares a sequence plays it here. The player only moves a modifier class on
  // the markup already mounted, so the elements keep their identity and the CSS transition on
  // the component does the tweening; this is the same mechanism a frame grabber steps by hand.
  useEffect(() => {
    const root = mounted.current;
    if (!root || !scene?.sequence?.length) return undefined;
    return playSequence(root, scene.sequence);
  }, [scene?.id, scene?.sequence]);

  if (!scene?.html) return null;
  return <figure className="au-screenshot-preview">
    <div
      className="au-product-frame au-screenshot-embed"
      style={{
        '--au-screenshot-width': String(scene.width),
        '--au-screenshot-height': String(scene.height),
        width: scene.width * scale,
        height: displayHeight,
        overflow: 'hidden',
        position: 'relative',
      }}
      aria-label={label}
    >
      <div
        className="au-screenshot-embed__scale"
        aria-hidden="true"
        style={{
          position: 'absolute',
          top: 0,
          left: 0,
          width: scene.width,
          height: scene.height,
          transform: `scale(${scale})`,
          transformOrigin: '0 0',
        }}
        ref={node => {
          mounted.current = node;
          if (node) node.inert = true;
        }}
        dangerouslySetInnerHTML={{ __html: scene.html }}
      />
    </div>
    <figcaption className="au-field-label">{label}</figcaption>
  </figure>;
}

export default function DesignSystemPage() {
  const surfaces = catalog.surfaces;
  const ids = useMemo(() => surfaces.map(surface => surface.id), [surfaces]);
  const screens = screenshots.screens;
  const screenIds = useMemo(() => screens.map(item => item.id), [screens]);
  const scenes = useMemo(
    () => Object.fromEntries(screenshots.scenes.map(scene => [scene.id, scene])),
    [],
  );
  const [active, setActive] = useState(ids[0]);
  const [activeScreen, setActiveScreen] = useState(screenIds.includes('applications') ? 'applications' : screenIds[0]);

  useEffect(() => {
    const sync = () => {
      const hash = window.location.hash.replace('#', '');
      if (ids.includes(hash)) setActive(hash);
      const selected = new URLSearchParams(window.location.search).get('screen');
      if (selected && screenIds.includes(selected)) setActiveScreen(selected);
    };
    sync();
    window.addEventListener('hashchange', sync);
    window.addEventListener('popstate', sync);
    return () => {
      window.removeEventListener('hashchange', sync);
      window.removeEventListener('popstate', sync);
    };
  }, [ids, screenIds]);

  const select = id => {
    setActive(id);
    window.history.replaceState(null, '', `#${id}`);
  };

  const selectScreen = id => {
    setActiveScreen(id);
    const url = new URL(window.location.href);
    url.searchParams.set('screen', id);
    window.history.replaceState(null, '', `${url.pathname}${url.search}${url.hash}`);
  };

  const surface = surfaces.find(item => item.id === active) ?? surfaces[0];
  const screen = screens.find(item => item.id === activeScreen) ?? screens[0];
  const desktop = screen.desktopId ? scenes[screen.desktopId] : null;
  const mobile = screen.mobileId ? scenes[screen.mobileId] : null;

  return <Layout title="Design System" description="The canonical Agent-Up product, interface, and marketing design system.">
    <main className={`au-theme ${styles.page}`}>
      <header>
        <div className={`au-container au-marketing-hero ${styles.intro}`}>
          <div>
            <p className="au-eyebrow">Agent-Up design system</p>
            <h1 className={`au-display ${styles.introTitle}`}>One design schema</h1>
            <p className="au-lede">Every app, every docs appearance, every post and everything else uses the same design schema.</p>
            <div className="au-cluster">
              <Link className="au-button au-button--secondary" to="/developer-guide/repo/design-system">Implementation contract</Link>
            </div>
          </div>
          <IntroPlayground />
        </div>
      </header>

      <section className={`au-section ${styles.overview}`} id="screens">
        <div className={styles.overviewInner}>
          <div className="au-screenshot-showcase">
            <div className={styles.overviewHead}>
              <p className="au-eyebrow">Assembled screens</p>
              <h2 className={`au-title ${styles.overviewTitle}`}>One central definition.</h2>
              <p className="au-lede">Each screen is defined centrally and itself assembled from central component definitions.</p>
            </div>

            <div className={styles.screenNav}>
              <nav aria-label="Assembled screens">
                <div className={styles.screenChipRow} role="tablist" aria-orientation="horizontal">
                  {screens.map(item => (
                    <button
                      key={item.id}
                      id={`screen-tab-${item.id}`}
                      type="button"
                      className={`au-chip${item.id === activeScreen ? ' au-chip--selected' : ''} ${styles.screenChip}`}
                      role="tab"
                      aria-selected={item.id === activeScreen}
                      aria-controls={`screen-panel-${item.id}`}
                      onClick={() => selectScreen(item.id)}
                    >
                      {item.title}
                    </button>
                  ))}
                </div>
              </nav>

              <section
                id={`screen-panel-${screen.id}`}
                className={styles.screenPanel}
                role="tabpanel"
                aria-labelledby={`screen-tab-${screen.id}`}
              >
                <div className={styles.screenCopy}>
                  <h3 className="au-page-title">{screen.title}</h3>
                  <p className="au-muted">{screen.intro}</p>
                </div>
                <div className="au-screenshot-stage">
                  <div className="au-screenshot-pair">
                    {desktop ? <ScenePreview scene={desktop} label="Desktop" /> : null}
                    {mobile ? <ScenePreview scene={mobile} label="Mobile" /> : null}
                  </div>
                </div>
              </section>
            </div>
          </div>
        </div>
      </section>

      <div className={`au-section ${styles.shell}`} id="catalog">
        <div className={`au-container ${styles.catalog}`}>
          <nav className={styles.surfaceNav} aria-label="Design system surfaces">
            <p className="au-eyebrow">Surfaces</p>
            <div className={styles.surfaceList} role="tablist" aria-orientation="vertical">
              {surfaces.map((item, index) => (
                <button
                  key={item.id}
                  id={`tab-${item.id}`}
                  className={`au-card${item.id === active ? ' au-card--selected' : ''} ${styles.surfaceTab}`}
                  role="tab"
                  aria-selected={item.id === active}
                  aria-controls={`panel-${item.id}`}
                  onClick={() => select(item.id)}
                >
                  <span className="au-mono au-muted">{String(index + 1).padStart(2, '0')}</span>
                  <span className={styles.surfaceCopy}>
                    <strong className="au-heading">{item.title}</strong>
                    <small className="au-muted">{item.intro}</small>
                    <em className="au-mono au-muted">{item.components.length} {item.components.length === 1 ? 'example' : 'examples'}</em>
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
            <h2 className="au-page-title">{surface.title}</h2>
            <p className="au-muted">{surface.intro}</p>

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
                <article className="au-card" key={label}><p className="au-eyebrow">{label}</p><p className={`au-lede ${styles.voiceCopy}`}>{copy}</p></article>
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
