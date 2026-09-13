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
not edit them directly. Change the canonical CSS or HTML catalog, run
`./au-debug build design-system`, and review every consumer.

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

- Near-black (`#0a0b0c`) is the canvas. Desktop rails and the Mobile drawer stay
  on that canvas. Surfaces step up through `surface` / `surface-raised` /
  `surface-overlay` so cards, fields, and overlays read as containers rather
  than vanishing into the page.
- A tappable card, such as an agent picker row, is `.au-choice`. It is a
  catalog Button that already paints as a card, including hover and disabled
  opacity. Do not wrap `.au-card` in a platform Button.
- Agent replies are `.au-card`. Tools and other in-run steps are `.au-chat-work`
  on the quieter surface. The human prompt is `.au-chat-user`: a right-aligned
  selected surface sized to the text. Finished work between questions collapses
  to `.au-chat-run`; the trailing agent reply stays visible. Nested thoughts stay
  `.au-chat-thought`. Do not put thoughts or tool rows in a result card.
- Borders are **alpha hairlines**, not fixed grays. An opaque border reads about
  2.4x stronger on the canvas than on a raised surface; alpha composites, so one
  token keeps an even weight across the whole ramp. A product drawn mostly in
  borders reads as a wireframe grid of rectangles.
- **Interaction is neutral.** Hover and pressed use `state-hover` and
  `state-active` — white at 6% and 10%. Painting the brand colour into every
  interaction state is the Material signature and is forbidden; a test enforces
  it. An element that already rests on the accent may brighten it on hover.
- **Selection is meaning.** A selected row takes the accent tint plus a 2px
  accent rule on its leading edge. Saturated accent fills behind text are
  retired: muted text on the old fill measured 1.83:1.
- **Emphasis follows the information hierarchy.** The primary selection on a
  screen carries the accent — choosing an application is a 2px accent underline.
  Secondary selections, and many-of-many filter sets, stay neutral.
- Radius scales with the object: `xs`/`sm` for controls, `md` for rows, buttons
  and inputs, `lg` for panels and cards, `xl` for panes and dialogs. An 8px
  corner reads rounded on a chip and square on a 900px pane.
- Working regions are `.au-pane` — inset on the canvas with a container radius
  and one elevation step — not full-bleed panels butted together at 1px lines.
  Dialogs and menus use `.au-overlay-panel` above a scrim.
- Product chrome uses the **UI type tier** (`--au-font-size-ui-*`, 11-22px) and
  the **UI weight roles** (`--au-weight-ui` 500, `--au-weight-ui-strong` 600).
  The content tier and 700+ weights belong to docs and marketing; uniform 700 on
  11-13px labels is what reads as a template. Product screens use
  `.au-page-title` and `.au-field-label`, not marketing display type.
- Any surface made of digits — logs, tables, ports, timestamps, metrics — sets
  `font-variant-numeric: tabular-nums` so columns align.
- Off-white and muted gray-green establish text hierarchy. Every text role must
  clear WCAG AA on every fill the catalog puts it on; a test enforces it.
- Blue focus remains distinct from green success.
- Red identifies errors, failures, and destructive actions.
- Ambient neon glow, decorative green grids, green borders around every surface,
  and accent-tinted hover states are retired.
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
does not exactly match the canonical HTML and CSS. Native `font-style` compiles to
React Native `italic` or `normal`; CSS `oblique` maps to `italic` because React
Native does not accept `oblique`.
