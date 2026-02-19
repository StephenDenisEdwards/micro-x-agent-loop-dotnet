# ADR-001: DotNetEnv for Secrets Management

## Status

Accepted

## Context

The application requires API keys for Anthropic and Google OAuth credentials. These secrets must not be committed to source control. .NET offers several options for secret management:

1. **User Secrets** (`dotnet user-secrets`) — built-in, per-project, stored outside the repo
2. **Environment variables** — set at OS level, no file needed
3. **DotNetEnv** (`.env` file) — simple file-based approach, compatible with the original Node.js project
4. **Azure Key Vault / AWS Secrets Manager** — cloud-based, production-grade

The original `micro-x-agent-loop` (Node.js) project uses a `.env` file with the `dotenv` package. Keeping the same `.env` file format allows sharing credentials between both projects without reconfiguration.

## Decision

Use the `DotNetEnv` NuGet package to load secrets from a `.env` file at startup. The `.env` file is added to `.gitignore` to prevent accidental commits.

Non-secret configuration (model, max tokens, temperature, paths) lives separately in `appsettings.json`.

## Consequences

**Easier:**
- Share the same `.env` file between the Node.js and .NET versions
- Simple to set up — just create a file, no tooling required
- Familiar pattern for developers coming from Node.js/Python

**Harder:**
- No built-in rotation or encryption (acceptable for personal/development use)
- Must remember to create `.env` manually on new machines (a `.env.example` template is checked in to help)
- Not suitable for production deployment without additional secret management
