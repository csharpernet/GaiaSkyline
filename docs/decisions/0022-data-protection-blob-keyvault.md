# 0022 — Data Protection keys in Blob, wrapped by Key Vault

Date: 2026-10-06 · Status: accepted · Stage 8 Part B

## Context

ASP.NET Core Data Protection keeps its key ring in process memory by default. That ring protects the
Identity auth cookie, the antiforgery token, the guest magic-link / partner-invite / booking-confirmation
tokens, the content-preview and booking-access cookies, and the encrypted site settings (external iCal URLs
and the pricing API key). In production the app runs Always On, is deployed by swapping a staging slot into
production, and may scale out — each of which starts a fresh process with a fresh in-memory ring, which
would invalidate every one of those the moment it happens. The spec makes persisting the ring mandatory.

## Decision

- The key ring is **persisted to the `dataprotection` Blob container** (`keys.xml`) and **wrapped at rest
  with a Key Vault RSA key** (`ProtectKeysWithAzureKeyVault`), both reached through the App Service
  **system-assigned managed identity** — no storage keys or vault secrets in configuration.
- Every instance and both deployment slots use the **same application name** (`GaiaSkyline`), so a key one
  instance creates is readable by all others and the ring is **not regenerated on a slot swap**.
- In **Production the Azure bindings are mandatory**: if `Azure:StorageAccountName`, `Azure:KeyVaultUri` or
  `Azure:DataProtectionKeyName` is missing, startup throws rather than silently using an ephemeral ring.
- **Development and CI keep the default in-process ring** — a single instance with nothing to share, and no
  Azure dependency in the local loop.

## Consequences

- Tokens and cookies survive restarts, slot swaps and scale-out. The staging slot shares the production
  ring, so a swap never signs everyone out.
- The managed identity needs **Storage Blob Data Contributor** on the account and **Key Vault Crypto User**
  on the vault (granted in the Bicep).
- Rotating or losing the Key Vault key makes the existing ring unreadable; key rotation is an operator
  procedure documented in the runbook (Part F). Soft-delete + purge protection on the vault guard against
  accidental loss.
- The invariant is covered without a cloud dependency by a cross-instance decrypt test over a shared
  persisted store (`DataProtectionSharingTests`).
