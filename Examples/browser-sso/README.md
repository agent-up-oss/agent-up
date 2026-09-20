# Browser SSO example

A tiny identity front door that speaks the Agent-Up client connection contract. It is not a hosted product, not a cloud control plane, and not an identity-vendor SDK.

The OSS Agent-Up Server authenticates with a local administrator password and always returns a community entitlement document with every operation available. Some self-hosted deployments sit a browser identity step in front of that surface instead. This example is that front door:

1. `GET /api/connection` advertises `authentication.mode = browserSso`.
2. Mobile opens `GET /api/auth/sso?redirect_uri=…` in the browser.
3. The front door issues an access token on a **loopback** redirect (`?access_token=`).
4. `GET /api/entitlements` is the permission document. Clients hide UI from feature keys such as `workspace.create`; they must not branch on edition names.

## Run it

```bash
node server.mjs
```

The process listens on `http://127.0.0.1:8787` (`PORT` overrides that). In Agent-Up Mobile, add that URL as a Server. Mobile reads the connection document, shows browser sign-in instead of a password, and hides Add workspace because this example sets `workspace.create` to unavailable.

## Tests

```bash
npm test          # HTTP journey: connection, SSO redirect, entitlements
npm run test:e2e  # Playwright: real Mobile web export against this process
```

`npm test` is the fast contract. Playwright is wired into Mobile CI after the web export (`AGENTUP_MOBILE_WEB_EXPORT`). `./au-debug test mobile` runs the HTTP tests.
