# Exemplos YAML

## Workflow: Feature branch → develop
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
						// ...ver exemplo no repositório...
```

## Workflow: develop → main (release)
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
						// ...ver exemplo no repositório...
```

# Requisitos de segurança e permissions

- O secret `REPO_BOT_TOKEN` deve ser um PAT com escopos mínimos: `repo` (e `workflow` se quiser rerun via API).
- Nunca exponha o token em logs ou código.
- Use expiração curta e rotacione periodicamente.
- Prefira criar o PAT com um usuário bot ou service account, não pessoal.
- Se o repositório for de organização, considere usar Organization secrets.

# Proteções de branch e merge policy

- Ative branch protection para `main` e `develop`:
	- Exigir PRs para merge.
	- Exigir status checks obrigatórios (build, test, lint, etc).
	- Exigir pelo menos 1 review (ou mais, conforme política).
	- Bloquear force-push e deleção de branch.
- Use CODEOWNERS para áreas críticas:
	- Exemplo de arquivo `.github/CODEOWNERS`:
		```
		*       @josemaramorim @squad-backend
		src/   @squad-backend
		docs/  @tech-leads
		```
- Considere ativar auto-merge apenas para PRs com todos os checks e reviews aprovados.

# Observações finais

- O fluxo foi testado e validado: feature → develop → main, com automação de PRs e CI resiliente.
- Para dúvidas ou ajustes, consulte este arquivo ou abra uma issue.
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

If you want, I can also add branch protection suggestions and a sample `CODEOWNERS` file.
