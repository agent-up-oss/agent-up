# Agent-Up Design System

This package is the single source of truth for Agent-Up product UI, documentation,
and marketing presentation. The canonical format is HTML and CSS:

- `src/agent-up.css` defines foundations, semantic tokens, accessibility defaults,
  layout primitives, typography, and reusable product components.
- `src/marketing.css` defines reusable marketing compositions that keep the real
  product UI as the visual reference.
- `brand/voice.json` defines product naming, positioning, lifecycle language, and
  editorial rules.

Websites import the CSS directly. `npm run build` deterministically compiles CSS
custom properties into the React Native module and Avalonia resource dictionary
under `dist/`. Those generated bindings must never be edited manually.

## Consumer contract

```css
@import '@agent-up/design-system/styles.css';
@import '@agent-up/design-system/marketing.css';
```

```ts
import { agentUpTheme } from '@agent-up/design-system/native';
```

Desktop includes `dist/avalonia/AgentUpTheme.axaml` as an Avalonia resource.
External marketing repositories can add this directory as a Git submodule and
depend on `@agent-up/design-system` through a local `file:` dependency.

Run `npm test` to verify generated bindings, required public contract sections,
and the no-neon/no-ambient-glow marketing rule.
