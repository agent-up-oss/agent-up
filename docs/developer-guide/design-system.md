---
title: Design System
sidebar_position: 12
---

# Design system

`AgentUp.DesignSystem/` is the single source of truth for Agent-Up product UI,
documentation, and marketing presentation. Its canonical format is HTML and CSS
so another repository can consume the same contract through an npm dependency or
an Agent-Up Git submodule.

The public showcase is available at [/design-system](/design-system). It is linked
from the site navbar and footer. The page is a tabbed catalog: a vertical
surface list sits in the left of the content column, and each selected surface
shows the live components that surface uses.

## Ownership and sources

- `src/agent-up.css` owns semantic colors, typography, spacing, shape,
  accessibility defaults, layout primitives, and reusable interface components.
- `src/product.css` owns Agent-Up product surfaces such as chrome, workspaces,
  application tabs, browser, console, Git, diagnostics, metrics, validation,
  sign-in, and Mobile.
- `src/catalog.html` owns the HTML structure Desktop, Mobile, docs, and the
  showcase infer from. Avalonia control types and Desktop class aliases are
  declared on each catalog example.
- `src/marketing.css` owns campaign and product-frame compositions.
- `brand/voice.json` owns product naming, positioning, capability lifecycle
  language, and editorial principles.
- `scripts/build.mjs` deterministically copies web assets and compiles the CSS
  custom properties **and class rules** into React Native objects and Avalonia
  resources plus styles under `dist/`.

Files under `AgentUp.DesignSystem/dist/` are generated definition artifacts. Do
not edit them directly. Change the canonical CSS or HTML catalog, run the
design-system build, and review every consumer.

## Consumer boundaries

The documentation site imports the CSS package directly. Mobile imports
`agentUpTheme`, `auBox`, and `auText` from `@agent-up/design-system/native`
and applies those compiled component styles to chrome, rows, cards, buttons,
and fields. Mobile must not invent a second look from color tokens. Desktop
includes the generated `AgentUpTheme.axaml` resource dictionary **and**
`AgentUpStyles.axaml` style sheet. Desktop applies catalog classes such as
`wsEntry`, `au-sign-in`, and `au-button` instead of restating fill, radius, and
border as local XAML. It keeps only ControlTemplates or optical adjustments
that CSS cannot express.

These are hard dependencies rather than examples copied into each project. A
change to the canonical CSS or catalog is incomplete until all generated
bindings are current and the Desktop, Mobile, docs, design showcase, and
marketing-template checks pass.

## Visual rules

Desktop is the reference rendering:

- Black is the uninterrupted canvas.
- Raised surfaces (`#121212` / `#1c1c1c`) sit on that canvas so cards, rails,
  and fields read as Material containers rather than vanishing into the page.
- Neutral gray borders define structure. Selected cards tint the fill; they do
  not grow a green outline.
- Product screens use `.au-page-title` (22px) and `.au-field-label` (12px).
  Marketing `.au-title` and `.au-lede` stay on campaign pages.
- Off-white and muted gray-green establish text hierarchy.
- Green is limited to primary action, the selected workspace fill, progress,
  and healthy state.
- Blue focus remains distinct from green success.
- Red identifies errors, failures, and destructive actions.
- Ambient neon glow, decorative green grids, and green borders around every
  surface are retired.
- Product UI and real product screenshots are preferred to speculative
  illustrations.

Mobile uses the same sign-in card, workspace row, and type scale as Desktop.
Primary actions stay 44px; chrome stays compact. Documentation prioritizes
reading. Marketing gets one focal point, one outcome, and a visible
`Available`, `Preview`, `Experimental`, or `Planned` label when it describes a
capability.

## Commands

```bash
npm --prefix AgentUp.DesignSystem run build
npm --prefix AgentUp.DesignSystem run check
npm --prefix AgentUp.DesignSystem test
```

The `check` command fails when a generated web, React Native, or Avalonia binding
does not exactly match the canonical HTML and CSS.
