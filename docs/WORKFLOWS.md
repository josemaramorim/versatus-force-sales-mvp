# CI / PR Workflows

This document describes the recommended workflow for this repository.

1) Feature branches
- Push a feature branch (any name except `develop`/`main`).
- Open PR manually from feature -> `develop`.
- CI runs in `.github/workflows/ci.yml` on PR and validates docs, build and integration/e2e.

2) PR to `develop`
- Every PR to `develop` triggers CI (`.github/workflows/ci.yml`).
- `develop` is the integrator branch and MUST have branch protection: required checks and reviews.

3) Release PR `develop -> main`
- When `develop` receives new commits (push), the workflow `.github/workflows/ci-create-release-pr.yml` will create a PR `develop -> main` if none exists.
- Do not auto-merge `develop -> main` without required approvals and passing checks. Use GitHub's auto-merge feature if you want automatic merge after checks+approvals.

4) Sync PR `main -> develop`
- When `main` receives commits, `.github/workflows/sync-main-to-develop.yml` opens a sync PR `main -> develop` if none exists.
- This avoids direct pushes to protected branches and keeps history aligned.

5) Tokens and permissions
- Workflows use `GITHUB_TOKEN` by default. If your org blocks `GITHUB_TOKEN` for certain write actions, create a minimal PAT (`repo`, `workflow`) and store in `secrets.REPO_BOT_TOKEN`.

6) Notes and safeguards
- The workflows check for existing open PRs before creating a new one to avoid duplicates.
- Avoid auto-resolving conflicts without human review.
- Keep `main` and `develop` protected (required checks, code owners, no forced pushes).
---

## MVP Feature Branch Strategy (Merge-Friendly PRs)

**Rule**: Each task must fit in <= 1 day of work. Each user story must be deliverable in <= 3 days via incremental PRs.

### Branch naming

```
feature/001-fluxo-e2e-<slug>   # e.g. feature/001-fluxo-e2e-us1-auth
bugfix/001-<short-description>
```

### PR size guidelines

| Concern | Limit |
|---|---|
| Production files per PR | <= 5 |
| Test files per PR | <= 3 |
| Lines changed | <= 400 (soft limit) |
| Stories per PR | 1 |

### Merge gates per story

| PR | Required checks before merge |
|---|---|
| PR-US1 (Auth+Licenca) | T014-T018 passing; login by email without tenant field; no cross-tenant leak |
| PR-US2 (Catalogo+Pedidos) | T028-T031 passing; catalog filtered by tenant; order totals correct |
| PR-US3 (ERP Async) | T042-T044 passing; idempotency confirmed on duplicate and out-of-order returns |
| PR-US4 (Demo) | T054-T055 passing; demo script runs without code changes |

### Contract change policy

- REST contracts (`contracts/rest-e2e-vendas.openapi.yaml`) and event schemas (`contracts/eventos-integracao-pedidos.schema.json`) must NOT be changed without:
  1. Bumping the contract version (OpenAPI `info.version` or JSON Schema `$id` path).
  2. Documenting the transition window in `quickstart.md` and in the OpenAPI `x-contract-migration` extension.
- Breaking changes require a deprecation period defined before merge.

### Workflow for a feature task

```text
1. git checkout -b feature/001-fluxo-e2e-<slug> develop
2. Implement task (code + test)
3. Run: dotnet test src/backend/Versatus.ForcaVendas.Api.Tests/
4. Push branch -> CI runs automatically
5. Open PR to develop with story reference in title
6. Await CI green + 1 review
7. Squash merge to develop
```

### Mandatory SDD branch gate

- For every SDD user story (US1-US4), branch creation is mandatory before writing code.
- Never implement feature tasks directly on `develop` or `main`.
- Suggested story branches:
	- `feature/001-fluxo-e2e-us1-auth`
	- `feature/001-fluxo-e2e-us2-pedidos`
	- `feature/001-fluxo-e2e-us3-integracao`
	- `feature/001-fluxo-e2e-us4-demo`
- If a story requires multiple PRs, use suffixes: `-part1`, `-part2`, etc.
## YAML snippets (reference)

Feature branch -> develop (manual PR + CI on pull_request):

```yaml
on:
	pull_request:
		branches: [develop, main]
jobs:
	ci:
		runs-on: ubuntu-latest
		steps:
			- uses: actions/checkout@v4
			- uses: actions/setup-dotnet@v4
				with:
					dotnet-version: 8.0.x
			- run: dotnet restore
			- run: dotnet build --configuration Release --no-restore
			- run: dotnet test --no-build --verbosity normal
```

develop -> main release:

```yaml
on:
	push:
		branches: [develop]
jobs:
	create-release-pr:
		runs-on: ubuntu-latest
		steps:
			- uses: actions/checkout@v4
			- name: Create develop -> main PR
				if: github.event_name == 'push'
				continue-on-error: true
				uses: actions/github-script@v6
				with:
					github-token: ${{ secrets.REPO_BOT_TOKEN }}
					script: |
						// ...see repo workflow example...
```
