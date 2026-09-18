# Agent-Up Design System

This package is the single source of truth for Agent-Up product UI, documentation,
and marketing presentation. The canonical format is HTML and CSS:

- `src/agent-up.css` defines foundations, semantic tokens, accessibility defaults,
  layout primitives, typography, and reusable product components.
- `src/product.css` defines Agent-Up product surfaces: chrome, workspaces,
  applications, browser, console, Git, diagnostics, metrics, validation, auth,
  database explorer, and Mobile.
- `src/catalog.html` is the structural catalog. Additional `src/catalog-*.html`
  fragments (Git history in `catalog-git-log.html`) are concatenated at build
  time. Avalonia infers control types, class names, and Desktop aliases from the
  combined catalog instead of restating the same UI in XAML.
- `src/git-log.css` defines the commit graph, history row, and ref chips.
- `src/marketing.css` defines reusable marketing compositions that keep the real
  product UI as the visual reference.
- `src/docs.css` defines documentation attention components used by User Docs
  and the Developer Guide. It is not compiled into Desktop or Mobile bindings.
- `brand/voice.json` defines product naming, positioning, lifecycle language, and
  editorial rules.

Websites import the CSS directly. `npm run build` compiles CSS custom properties
and class rules into React Native style objects and Avalonia resources **and
styles** under `dist/`. Those generated bindings must never be edited manually.

## Consumer contract

```css
@import '@agent-up/design-system/styles.css';
@import '@agent-up/design-system/marketing.css';
@import '@agent-up/design-system/docs.css';
```

```ts
import { agentUpTheme } from '@agent-up/design-system/native';
import catalog from '@agent-up/design-system/catalog';
```

Desktop includes `dist/avalonia/AgentUpTheme.axaml` as resources and
`dist/avalonia/AgentUpStyles.axaml` as application styles. External marketing
repositories can add this directory as a Git submodule and depend on
`@agent-up/design-system` through a local `file:` dependency.

The public showcase is `/design-system` on the documentation site. It is linked
from the navbar and renders the catalog as tabs, one Agent-Up surface per tab.

Run `npm test` to verify generated bindings, required catalog surfaces, inferred
Avalonia styles, the no-neon/no-ambient-glow marketing rule, and the two rules
that keep the accent meaningful:

- **Interaction stays neutral.** No `:hover` rule may paint an accent fill on a
  control that does not already rest on the accent. Hover and pressed use
  `--au-color-state-hover` / `--au-color-state-active`.
- **Text stays legible on every fill.** Each text role must clear WCAG AA
  (4.5:1) against every surface and selection fill the catalog places it on.

See `docs/developer-guide/repo/design-system.md` for the full visual contract.
