# Agent-Up Design System

This package is the single source of truth for Agent-Up product UI, documentation,
and marketing presentation. The canonical format is HTML and CSS:

- `src/agent-up.css` defines foundations, semantic tokens, accessibility defaults,
  layout primitives, typography, and reusable product components.
- `src/product.css` defines Agent-Up product surfaces: chrome, workspaces,
  applications, browser, console, Git, diagnostics, metrics, validation, auth,
  database explorer, and Mobile.
- `src/catalog.html` is the structural catalog. Avalonia infers control types,
  class names, and Desktop aliases from it instead of restating the same UI in XAML.
- `src/marketing.css` defines reusable marketing compositions that keep the real
  product UI as the visual reference.
- `brand/voice.json` defines product naming, positioning, lifecycle language, and
  editorial rules.

Websites import the CSS directly. `npm run build` compiles CSS custom properties
and class rules into React Native style objects and Avalonia resources **and
styles** under `dist/`. Those generated bindings must never be edited manually.

## Consumer contract

```css
@import '@agent-up/design-system/styles.css';
@import '@agent-up/design-system/marketing.css';
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
Avalonia styles, and the no-neon/no-ambient-glow marketing rule.
