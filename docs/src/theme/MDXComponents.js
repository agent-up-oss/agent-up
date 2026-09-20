import React from 'react';
import Link from '@docusaurus/Link';
import MDXComponents from '@theme-original/MDXComponents';

const lifecycleLabels = {
  available: 'Available',
  preview: 'Preview',
  experimental: 'Experimental',
  planned: 'Planned',
};

const calloutLabels = {
  info: 'Note',
  warning: 'Watch',
  danger: 'Do not',
};

function hasContent(value) {
  if (value == null || value === false) return false;
  if (typeof value === 'string') return value.trim().length > 0;
  if (Array.isArray(value)) return value.some(hasContent);
  return true;
}

function DocEyebrow({ slice, status = 'available' }) {
  const label = lifecycleLabels[status] ?? lifecycleLabels.available;
  return (
    <p className="au-cluster au-doc-kicker">
      <span className="au-field-label">{slice}</span>
      <span className={`au-badge au-doc-lifecycle au-doc-lifecycle--${status}`}>{label}</span>
    </p>
  );
}

function DocFocus({ label = 'Watch', children }) {
  return (
    <aside className="au-pane au-doc-focus">
      {label ? <p className="au-field-label">{label}</p> : null}
      <div className="au-doc-focus__body">{children}</div>
    </aside>
  );
}

function DocMeta({ owner, tests, mcp, rest }) {
  const rows = [
    owner && ['Owner', owner],
    tests && ['Tests', tests],
    mcp && ['MCP', mcp],
    rest && ['REST', rest],
  ].filter(Boolean);
  return (
    <div className="au-doc-meta">
      {rows.map(([name, value]) => (
        <div key={name} className="au-doc-meta__row">
          <p className="au-field-label">{name}</p>
          <p className="au-doc-meta__value">{value}</p>
        </div>
      ))}
    </div>
  );
}

function DocWhat({ label, children }) {
  return (
    <div className="au-doc-what">
      {label ? <p className="au-field-label">{label}</p> : null}
      <div className="au-doc-what__body">{children}</div>
    </div>
  );
}

function DocSpine({ label, children }) {
  return (
    <div className="au-doc-spine">
      {label ? <p className="au-field-label">{label}</p> : null}
      <ol className="au-doc-spine__list">{children}</ol>
    </div>
  );
}

function DocBeat({ selected, title, children }) {
  const heading = title ?? children;
  const body = title ? children : null;
  return (
    <li className={selected ? 'au-doc-spine__beat au-doc-spine__beat--selected' : 'au-doc-spine__beat'}>
      <span className="au-doc-spine__n" aria-hidden="true" />
      <div>
        <p className="au-doc-spine__title">{heading}</p>
        {hasContent(body) ? <div className="au-doc-spine__body">{body}</div> : null}
      </div>
    </li>
  );
}

function DocContract({ label = 'Contract', children }) {
  return (
    <div className="au-doc-contract">
      <p className="au-field-label">{label}</p>
      <pre className="au-code">{children}</pre>
    </div>
  );
}

function DocFork({ question, children }) {
  return (
    <div className="au-doc-fork">
      {question ? <p className="au-doc-fork__question">{question}</p> : null}
      {children}
    </div>
  );
}

function DocForkPath({ title, open, children }) {
  return (
    <details className="au-doc-fork__path" open={open}>
      <summary>{title}</summary>
      <div>{children}</div>
    </details>
  );
}

function DocFacts({ label = 'Keep in view', children }) {
  return (
    <div className="au-doc-facts">
      {label ? <p className="au-field-label">{label}</p> : null}
      <div className="au-doc-facts__list">{children}</div>
    </div>
  );
}

function DocFact({ label, children }) {
  return (
    <div className="au-doc-fact">
      <p className="au-field-label">{label}</p>
      <div className="au-doc-fact__value">{children}</div>
    </div>
  );
}

function surfaceName({ desktop, mobile, cli, mcp }) {
  if (desktop) return 'Desktop';
  if (mobile) return 'Mobile';
  if (cli) return 'CLI';
  if (mcp) return 'MCP';
  return 'Surface';
}

function DocSurfaces({ label = 'Where it differs', children }) {
  return (
    <div className="au-doc-surfaces-wrap">
      {label ? <p className="au-field-label">{label}</p> : null}
      <div className="au-doc-surfaces">{children}</div>
    </div>
  );
}

function DocSurface(props) {
  const { children, ...flags } = props;
  return (
    <div className="au-doc-surface">
      <p className="au-field-label">{surfaceName(flags)}</p>
      <div className="au-doc-surface__body">{children}</div>
    </div>
  );
}

function DocSteps({ children }) {
  return <ol className="au-doc-steps">{children}</ol>;
}

function DocStep({ title, children }) {
  return (
    <li className="au-doc-step">
      <span className="au-doc-step__n" aria-hidden="true" />
      <div>
        {title ? <p className="au-doc-step__title">{title}</p> : null}
        {hasContent(children) ? <div className="au-doc-step__body">{children}</div> : null}
      </div>
    </li>
  );
}

function DocCallout({ kind = 'info', label, children }) {
  const modifier = kind === 'warning' ? ' au-callout--warning' : kind === 'danger' ? ' au-callout--danger' : '';
  return (
    <aside className={`au-callout au-doc-callout${modifier}`}>
      <p className="au-field-label">{label ?? calloutLabels[kind] ?? calloutLabels.info}</p>
      <div className="au-doc-callout__body">{children}</div>
    </aside>
  );
}

function DocNext({ href, title, children }) {
  const linkText = title ?? children;
  const hint = title ? children : null;
  return (
    <div className="au-doc-next">
      <p className="au-field-label">Next in this slice</p>
      <Link className="au-doc-next__link" to={href}>{linkText}</Link>
      {hasContent(hint) ? <p className="au-doc-next__hint">{hint}</p> : null}
    </div>
  );
}

function DocLifecycle({ status = 'available' }) {
  const label = lifecycleLabels[status] ?? lifecycleLabels.available;
  return <span className={`au-badge au-doc-lifecycle au-doc-lifecycle--${status}`}>{label}</span>;
}

export default {
  ...MDXComponents,
  DocFocus,
  DocSpine,
  DocBeat,
  DocFork,
  DocForkPath,
  DocContract,
  DocSurface,
  DocSurfaces,
  DocLifecycle,
  DocEyebrow,
  DocMeta,
  DocWhat,
  DocFacts,
  DocFact,
  DocSteps,
  DocStep,
  DocCallout,
  DocNext,
};
