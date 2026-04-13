# CI / PR Workflows

This document describes the recommended workflow for this repository.

1) Feature branches
- Push a feature branch (any name except `develop`/`main`).
- CI runs (`.github/workflows/ci-feature-create-pr.yml`) performing build/test/lint.
- If CI succeeds and there is no open PR from the branch to `develop`, the workflow will create a PR `feature -> develop` automatically.

2) PR to `develop`
- The PR created above triggers the CI configured for PRs into `develop` (integration/e2e).
- `develop` is the integrator branch and MUST have branch protection: required checks and reviews.

3) Release PR `develop -> main`
- When `develop` receives new commits (push), the workflow `.github/workflows/ci-create-release-pr.yml` will create a PR `develop -> main` if none exists.
- Do not auto-merge `develop -> main` without required approvals and passing checks. Use GitHub's auto-merge feature if you want automatic merge after checks+approvals.

4) Tokens and permissions
- Workflows use `GITHUB_TOKEN` by default. If your org blocks `GITHUB_TOKEN` for certain write actions, create a minimal PAT (`repo`, `workflow`) and store in `secrets.REPO_BOT_TOKEN`.

5) Notes and safeguards
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
## YAML snippets (reference)

Feature branch -> develop:

```yaml
on:
	push:
		branches-ignore: [main, develop]
jobs:
	build-test:
		runs-on: ubuntu-latest
		steps:
			- uses: actions/checkout@v4
			- uses: actions/setup-dotnet@v4
				with:
					dotnet-version: 8.0.x
			- run: dotnet restore
			- run: dotnet build --configuration Release --no-restore
			- run: dotnet test --no-build --verbosity normal
			- name: Create PR to develop
				if: github.event_name == 'push'
				continue-on-error: true
				uses: actions/github-script@v6
				with:
					github-token: ${{ secrets.REPO_BOT_TOKEN }}
					script: |
						// ...see repo workflow example...
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
