Based on registry `picomatch@4.0.7`.

`lib/parse.js` collapses a single-branch repeated wildcard extglob
(`+(*a)`, `+(*)`, `+(a*)b`) so it does not compile to a nested
unbounded quantifier (GHSA-fc5q-39xc-vjpf / CVE-2026-112366). The
change matches micromatch/picomatch#267.

`lib/utils.js` `removeBackslashes` is a linear scan of the same
`(?:\[.*?[^\\]\]|\\(?=.))` rules. Vendoring the original regex put a
CodeQL polynomial-regex finding on overlay input; the scan keeps
character classes, drops unescaped backslashes, and stops looking for
a closer once no unescaped `]` remains.

Remove this overlay when registry picomatch 4.0.8 or later is
published and the advisory lists that version as patched.
