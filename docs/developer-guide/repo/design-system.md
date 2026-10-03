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
from the site navbar and footer. The page opens with the contract intro beside a
live collage of catalog product-control examples. Those examples are the same
`component.html` definitions as the catalog below, so the intro stays in sync
when the catalog changes. Assembled Desktop and Mobile screens follow, selected
by a horizontal chip strip. Those views mount assembled screen HTML: `src/screens.html` layouts that
`data-au-use` catalog component ids. Desktop and Mobile sit side by side at
matched height and use the full page width. They do not embed persisted PNGs and they
do not restate component markup. Below that, a vertical
surface list sits in the left of the content column, and each selected surface
shows the live components that surface uses.

## Ownership and sources

- `src/agent-up.css` owns semantic colors, typography, spacing, shape,
  accessibility defaults, layout primitives, and reusable interface components.
- `src/product.css` owns Agent-Up product surfaces such as chrome, workspaces,
  application tabs, browser, console, Git, diagnostics, metrics, validation,
  sign-in, and Mobile.
- `src/catalog.html` owns the shared HTML structure Desktop, Mobile, docs, and the
  showcase infer from. Surface-specific fragments such as
  `src/catalog-git-log.html` and `src/catalog-file-viewer.html` are concatenated
  at build time so Git history and file inspection can evolve without colliding
  with the working-tree catalog. Avalonia control types and Desktop class aliases
  are declared on each catalog example.
- `src/git-log.css` owns the Git history graph, pinned timestamp column, sticky selected detail, and ref chips.
  Lane colors are the `--au-color-git-lane-*` tokens in `src/agent-up.css`.
- `src/file-viewer.css` owns the readonly inspection window: header, hunk
  navigation, gutter, line kinds, and syntax token colors. Grammars live in
  `src/syntax/highlight.mjs` and compile to Mobile and Desktop.
- `src/docs.css` owns documentation structure (kicker, focus, what-it-is,
  numbered spine, contract, fork, facts, surface rows, steps, callout, next).
  It is not compiled into Desktop or Mobile bindings.
- `src/marketing.css` owns campaign and product-frame compositions.
- `src/screenshots/shell.css` owns the scaled showcase embed only. Screen chrome
  layout lives in `src/product.css` as `.au-screen*` so Desktop can apply the
  same classes.
- `src/screens.html` owns assembled screens: named layouts plus scenes that
  fill regions with `data-au-use` of catalog component ids, including the
  `screen*` layout shells. `scripts/lib/screens.mjs` inserts leaf catalog HTML
  and applies empty layout shells as wrappers. Persist writes the result into
  `media/`. Desktop aliases (`productScreen`, `productScreenRail`, …) compile
  from those same catalog classes.
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
- The Git change list is `.au-git-change-list` with `.au-git-row` rows.
  Filenames are `.au-git-change-name` (directories use `--directory`). Status
  glyphs are `.au-git-status` with `--added`, `--untracked`, `--deleted`,
  `--modified`, `--renamed`, `--conflicted`, and `--directory`. Directory depth
  uses `.au-git-tree-guide` hairlines and `.au-git-tree-toggle` chevrons
  (`--expanded` / `--collapsed`). Checkbox selection stays on the checkbox; the
  open file uses `--selected`. Do not color the filename with the change kind.
  Review counts use `.au-git-insertions` and `.au-git-deletions`. Counts on a primary Commit button use `--on-primary` so they take on-accent instead of accent-on-accent.
- Git history is `.au-git-log` with `.au-git-log-row` entries, a pinned
  `.au-git-log-time` column, `.au-git-log-graph` lanes in
  `.au-git-log-graph-scroll`, and a sticky `.au-git-log-detail` header for the
  selected commit. Ref chips stay in that header. Lanes use `--au-color-git-lane-*`.
  Do not render ASCII `| * |` glyphs.
- Validation is `.au-validation-sidebar`, a right rail on the selected
  application, not a full-screen pane. `.au-validation-flow` cards nest
  `.au-validation-stage` greater steps and `.au-validation-check` micro-checks.
  Glyphs use `.au-validation-glyph` with `--passed`, `--running`, and
  `--failed`; pending stays muted. Play is `.au-validation-play`, a
  selected-strong pill. Collapse uses `.au-workspace-add`. Do not paint a
  stage as a fat status card. The default rail is
  `.au-validation-sidebar--collapsed`.
- File inspection is `.au-file-viewer`: a readonly overlay with
  `.au-file-viewer-header` (path + status), `.au-file-viewer-nav` hunk jumps,
  `.au-file-viewer-line` rows, and `.au-syntax-*` tokens. Added lines use the
  selected-soft fill; deleted lines use the danger surface; the current line uses
  the selected tint plus a 2px accent rule. Code is preformatted so leading
  spaces and tabs stay visible. Do not dump a file into one text
  node, and do not paint syntax with accent green.
- Radius scales with the object: `xs`/`sm` for controls, `md` for rows, buttons
  and inputs, `lg` for panels and cards, `xl` for panes and dialogs. An 8px
  corner reads rounded on a chip and square on a 900px pane.
- Working regions are `.au-pane` — inset on the canvas with a container radius
  and one elevation step — not full-bleed panels butted together at 1px lines.
  Dialogs and menus use `.au-overlay-panel` above a scrim. The sign-in screen is
  a compact pane on the canvas (`.au-sign-in`), not an overlay. Saved-server URLs
  ellipsize (`.au-choice-label`) with the full URL in the tooltip, and the saved
  list (`.au-sign-in-list`) scrolls. Rows do not display credential state;
  selecting a saved server applies a stored sign-in.
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
- Product UI and design-system product screenshots are preferred to speculative
  illustrations. Persist those shots with `au-debug screenshots persist`; do not
  reconstruct Desktop or Mobile chrome in docs HTML. The marketing homepage
  is a left lockup plus `.au-feature-card` tiles on the right. The lockup
  title stays on one line and sits beside a 3x3 grid that fits a 1440x900
  viewport at 100% zoom, vertically centered with that grid. Feature tiles
  center the relevant catalog component, with
  preview sample data, in a fixed inset viewport instead of stretching a
  screenshot crop, and use one short `.au-muted` line instead of a subtitle. A click opens Desktop
  and Mobile shots together in `.au-feature-modal`, portaled to
  `document.body` so it stays a viewport overlay. The connect pane is the
  Multi-server tile. File viewer and other tooling overlays stay out of that
  grid.

Mobile uses the same sign-in pane, workspace row, and type scale as Desktop.
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
Native does not accept `oblique`. Screenshot HTML is generated in the same build from `src/screens.html`;
refresh `media/` with `au-debug screenshots persist` and prove the shots still
compose catalog component HTML with `au-debug screenshots validate`.
