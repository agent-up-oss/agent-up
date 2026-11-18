# npm override leaves

Local packages npm `overrides` resolve in place of vulnerable registry
leaves that have no compatible patched release.

- `picomatch` is 4.0.7 plus the GHSA-fc5q-39xc-vjpf collapse for
  single-branch repeated wildcard extglobs. Version `4.0.8-agentup.0`
  sits above the advisory range `<= 4.0.7`. Switch the overrides to
  registry `picomatch` once 4.0.8 or later is published.
- `stream-json` is a CommonJS overlay that implements the two paths
  Detox 20 and bunyamin 1.6 still `require`: `jsonl/Parser` and
  `streamers/StreamArray.js`. It does not ship Assembler, JSONC, or
  the pick/ignore/filter/replace filters. Version `3.9.0-agentup.0`
  sits above the advisory range `< 3.6.0`. Switch the overrides to
  registry `stream-json` once bunyamin and Detox consume 3.6 or later.

Do not add other packages here. Prefer a registry pin in
`package.json` `overrides` when the parent can take a patched release.
