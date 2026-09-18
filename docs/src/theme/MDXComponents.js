import React from 'react';
import MDXComponents from '@theme-original/MDXComponents';

const lifecycleLabels = {
  available: 'Available',
  preview: 'Preview',
  experimental: 'Experimental',
  planned: 'Planned',
};

function DocFocus({ children }) {
  return <aside className="au-pane au-doc-focus">{children}</aside>;
}

function DocSpine({ children }) {
  return <ol className="au-doc-spine">{children}</ol>;
}

function DocBeat({ selected, children }) {
  return (
    <li className={selected ? 'au-doc-spine__beat au-doc-spine__beat--selected' : 'au-doc-spine__beat'}>
      {children}
    </li>
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

function DocContract({ children }) {
  return <pre className="au-code au-doc-contract">{children}</pre>;
}

function DocSurface({ desktop, mobile, cli, mcp, children }) {
  const chips = [];
  if (desktop) chips.push('Desktop');
  if (mobile) chips.push('Mobile');
  if (cli) chips.push('CLI');
  if (mcp) chips.push('MCP');
  return (
    <p className="au-cluster">
      {chips.map(name => (
        <span key={name} className="au-badge au-doc-surface">{name}</span>
      ))}
      {children}
    </p>
  );
}

function DocLifecycle({ status = 'available' }) {
  const label = lifecycleLabels[status] ?? lifecycleLabels.available;
  return <span className={`au-badge au-doc-lifecycle au-doc-lifecycle--${status}`}>{label}</span>;
}

function DocEyebrow({ slice, status = 'available' }) {
  return (
    <p className="au-eyebrow au-cluster">
      {slice}
      <DocLifecycle status={status} />
    </p>
  );
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
  DocLifecycle,
  DocEyebrow,
};
