---
title: CI Configuration
---

# CI Configuration

The Agent-Up CI workflow runs on every push. This page documents the repository secrets and variables that control optional CI behavior. All signing and release steps degrade gracefully when credentials are absent — no secret is required for the workflow to complete.

Secrets are set under **Settings → Secrets and variables → Actions → Secrets**. Variables are set under the **Variables** tab in the same location.

## Signing — macOS

Requires an [Apple Developer Program](https://developer.apple.com/programs/) membership ($99/year).

You need two separate certificates from the Apple Developer portal: a **Developer ID Application** certificate (signs the individual Mach-O binaries inside the package) and a **Developer ID Installer** certificate (signs the `.pkg` itself). Export each as a `.p12` file with a password, then base64-encode it: `base64 -i cert.p12 | pbcopy`.

Notarization uses an app-specific password, not your Apple ID login password. Create one at [appleid.apple.com](https://appleid.apple.com) under **Sign-In and Security → App-Specific Passwords**.

| Secret | Value |
|---|---|
| `MACOS_APP_CERTIFICATE` | Base64-encoded Developer ID Application `.p12` |
| `MACOS_APP_CERTIFICATE_PASSWORD` | Password for the Developer ID Application `.p12` |
| `MACOS_INSTALLER_CERTIFICATE` | Base64-encoded Developer ID Installer `.p12` |
| `MACOS_INSTALLER_CERTIFICATE_PASSWORD` | Password for the Developer ID Installer `.p12` |
| `MACOS_NOTARIZE_APPLE_ID` | Apple ID email associated with the developer account |
| `MACOS_NOTARIZE_APP_SPECIFIC_PASSWORD` | App-specific password from appleid.apple.com |
| `MACOS_NOTARIZE_TEAM_ID` | Team ID from the Apple Developer portal (top-right of the Certificates page) |
| `KEYCHAIN_PASSWORD` | Any random string — used to protect the temporary keychain created on the runner |

Enable macOS signing by setting the repository variable `MACOS_SIGNING_ENABLED` to `true`.

## Signing — Windows

Requires an [Azure Trusted Signing](https://learn.microsoft.com/en-us/azure/trusted-signing/) account. This is Microsoft's HSM-backed cloud signing service — no hardware token required. Identity validation (same process as a traditional OV code-signing certificate) takes 1–5 business days.

Setup steps:
1. Create an Azure subscription and a Trusted Signing account resource (`Microsoft.CodeSigning/codeSigningAccounts`).
2. Complete identity validation in the Azure portal.
3. Create a **Certificate Profile** (choose Public Trust).
4. Create a **Service Principal** (App Registration) and assign it the `Trusted Signing Certificate Profile Signer` role on the account.
5. Generate a client secret for the service principal.

| Secret | Value |
|---|---|
| `AZURE_TENANT_ID` | Azure AD directory (tenant) ID |
| `AZURE_CLIENT_ID` | Service principal application (client) ID |
| `AZURE_CLIENT_SECRET` | Service principal client secret |
| `AZURE_TRUSTED_SIGNING_ENDPOINT` | Account endpoint URL, e.g. `https://eus.codesigning.azure.net/` |
| `AZURE_TRUSTED_SIGNING_ACCOUNT` | Trusted Signing account resource name |
| `AZURE_TRUSTED_SIGNING_CERT_PROFILE` | Certificate profile name |

Enable Windows signing by setting the repository variable `AZURE_SIGNING_ENABLED` to `true`.

## Signing — Linux

Linux packages are GPG-signed. No external account is required — generate a dedicated key pair locally.

```bash
gpg --full-gen-key          # RSA 4096, set a long expiry or none
gpg --list-secret-keys --keyid-format LONG   # note the key ID
gpg --export-secret-keys --armor <KEY_ID>    # copy the output into the secret
gpg --export --armor <KEY_ID> > packaging/linux/agent-up-signing.asc  # commit the public key
```

| Secret | Value |
|---|---|
| `GPG_SIGNING_PRIVATE_KEY` | Armored private key (`--export-secret-keys --armor`) |
| `GPG_SIGNING_PASSPHRASE` | GPG key passphrase |

Enable Linux signing by setting the repository variable `LINUX_SIGNING_ENABLED` to `true`.

The signing step produces a detached `agent-up-ubuntu-linux-x64.deb.asc` signature file alongside the `.deb`. Users can verify with:

```bash
gpg --import agent-up-signing.asc
gpg --verify agent-up-ubuntu-linux-x64.deb.asc agent-up-ubuntu-linux-x64.deb
```

## Release

The release job uses `GITHUB_TOKEN`, which GitHub provides automatically. No setup required.

Releases only run on `main` when semantic-release determines a new version is warranted based on [Conventional Commits](https://www.conventionalcommits.org/).

## Repository Variables

Variables control which signing steps run. They are not secrets and can be read freely in workflow `if:` conditions.

| Variable | Effect when set to `true` |
|---|---|
| `MACOS_SIGNING_ENABLED` | Enables real macOS signing (requires the macOS secrets above) |
| `AZURE_SIGNING_ENABLED` | Enables real Windows signing via Azure Trusted Signing (requires the Azure secrets above) |
| `LINUX_SIGNING_ENABLED` | Enables real Linux GPG signing (requires the GPG secrets above) |
| `SIGNING_SMOKE_TEST` | Runs a credential-free signing dry run on all platforms: ad-hoc `codesign` on macOS, self-signed certificate via `signtool` on Windows, throwaway GPG key on Linux. Useful for validating the signing pipeline before real credentials are available. |

`SIGNING_SMOKE_TEST` and the platform-specific `*_SIGNING_ENABLED` variables are mutually exclusive in intent. Setting both at the same time would sign files twice.
