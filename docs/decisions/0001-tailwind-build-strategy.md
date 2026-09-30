# 0001 — Tailwind CSS build strategy

- Status: Accepted
- Date: 2026-09-29
- Deciders: GaiaSkyline maintainers

## Context

We need Tailwind CSS compiled to a static stylesheet at build time so the Web project ships
only the utilities it uses. The stack note allowed either `Tailwind.Extension.AspNetCore` or a
Node build step. Requirements:

- Reproducible in CI and in `docker compose`.
- `dotnet build` produces the final CSS with no manual step.
- The design tokens must also exist as `tailwind.config.js` (the spec is explicit about this
  file), which is the classic Tailwind **v3** configuration shape.

## Decision

Use a **Node build step driven by the Tailwind CLI (v3.4)**, invoked from an MSBuild target.

- `src/GaiaSkyline.Web/package.json` pins `tailwindcss ^3.4` and defines `build:css` / `watch:css`.
- Tokens live in `tailwind.config.js` (v3 JS config, exactly as required).
- An MSBuild `TailwindBuild` target runs `npm install` (only when `node_modules` is missing) and
  `npm run build:css` before the C# build, incrementally (inputs: config + `Styles/app.css` +
  `Views/**/*.cshtml`; output: `wwwroot/css/app.css`).
- The compiled `wwwroot/css/app.css` is **git-ignored** (a generated artifact).

We rejected `Tailwind.Extension.AspNetCore`: it is a third-party wrapper whose behaviour we could
not pin as confidently as the official CLI, and it obscures the standard Tailwind workflow that
front-end contributors already know.

We chose Tailwind **v3** over v4 because the spec mandates `tailwind.config.js`; v4 moves
configuration into CSS (`@theme`) and treats the JS config as legacy.

## Consequences

- **+** Standard, well-documented Tailwind workflow; `dotnet build` yields the final CSS.
- **+** Fast incremental rebuilds; `npm run watch:css` for live dev.
- **−** Adds Node.js (LTS) as a build-time prerequisite. This is documented in the README and set
  up in CI and the Docker build stage. It is not a runtime dependency.
- Because the output is generated, a first build must run the Node step; the Dockerfile does this
  in a dedicated stage so the published image needs only the .NET runtime.
