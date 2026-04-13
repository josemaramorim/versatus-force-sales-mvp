<!--
Sync Impact Report
- Version change: template-unset -> 1.0.0
- Modified principles:
	- Principle 1 placeholder -> I. MVP Value Slice First
	- Principle 2 placeholder -> II. Tenant Isolation and Session Licensing
	- Principle 3 placeholder -> III. Contract-Driven Integration and Status Flow
	- Principle 4 placeholder -> IV. Test and CI Quality Gates
	- Principle 5 placeholder -> V. Observability and Operational Traceability
- Added sections:
	- Delivery Constraints and Security Baselines
	- Workflow and Traceability Governance
- Removed sections:
	- None
- Templates requiring updates:
	- ✅ .specify/templates/plan-template.md
	- ✅ .specify/templates/spec-template.md
	- ✅ .specify/templates/tasks-template.md
	- ⚠ pending .specify/templates/commands/*.md (directory not present in this repository)
- Deferred TODOs:
	- None
-->

# Versatus Force Sales MVP Constitution

## Core Principles

### I. MVP Value Slice First
Every change MUST deliver a complete, demonstrable business slice aligned to the core MVP flow:
login with email and password (tenant resolved internally), session and license control, order
creation, ERP dispatch, and status return. Work MUST be split into increments that can be validated
independently and merged without waiting for unrelated scope. Teams SHOULD defer non-MVP
capabilities until a documented business need and acceptance criteria exist. Rationale: fast
feedback and demonstrable value are mandatory for this project's delivery model.

### II. Tenant Isolation and Session Licensing
All backend and worker behavior MUST enforce tenant isolation for data, cache, events, and access
control. Tenant identity MUST be explicit in authentication, authorization, persistence boundaries,
and integration messages. Concurrent session limits and license rules MUST be enforced with Redis-
based controls, including deterministic rejection behavior when limits are exceeded. Implementations
SHOULD prefer fail-closed behavior on missing tenant context or invalid license state. Rationale:
multi-tenant SaaS correctness and contractual licensing are non-negotiable product constraints.

### III. Contract-Driven Integration and Status Flow
API and asynchronous integration contracts MUST be explicit, versioned, and testable before merge.
Any change that affects ERP-bound payloads, domain events, or status transitions MUST include
contract updates and backward-compatibility analysis. Order lifecycle states MUST remain observable
from creation through dispatch and processing result (success or error). Implementations SHOULD use
idempotent handlers for integration events and retries. Rationale: the MVP's core value depends on
reliable ERP integration and transparent status propagation.

### IV. Test and CI Quality Gates
No pull request may be merged unless CI is green and required checks pass. At minimum, each change
MUST include automated tests proportional to risk: unit tests for domain logic, integration tests
for cross-boundary behavior, and contract tests for API/event changes. Existing tests MUST NOT be
disabled to force a green pipeline. Critical-path changes (auth, tenant isolation, licensing,
order flow, ERP dispatch/status) SHOULD include regression coverage proving previous behavior is
preserved. Rationale: CI integrity is the primary safeguard against regressions in rapid increments.

### V. Observability and Operational Traceability
Services MUST emit structured logs and correlation identifiers that connect frontend actions, API
requests, worker processing, and integration outcomes for a single order flow. Failures in auth,
tenant resolution, licensing, message publication/consumption, and ERP status updates MUST be
logged with actionable context while avoiding sensitive data exposure. New integration points SHOULD
define metrics and traces required for operational diagnostics before production use. Rationale:
operability and supportability are required to run MVP pilots safely.

## Delivery Constraints and Security Baselines

- Backend APIs and integration endpoints MUST use authenticated and authorized access paths.
- Transport security MUST be enforced in deployed environments.
- Secrets and credentials MUST be managed outside source code and never hardcoded.
- Data access patterns MUST prevent cross-tenant reads/writes by construction and by tests.
- Performance-sensitive paths (catalog lookups, session checks, order submission) SHOULD define
	measurable targets and monitoring alerts before release.

## Workflow and Traceability Governance

- Every implementation item MUST start from a tracked GitHub issue or story with acceptance
	criteria and linked technical tasks.
- Pull requests MUST be small and reviewable, reference their issue/story, and describe verification
	evidence (tests, logs, contracts, screenshots when applicable).
- Story/task decomposition MUST preserve independent testability and incremental delivery.
- Application composition roots and entrypoints (for example `Program.cs`) MUST remain thin and
	orchestration-only. Business rules and endpoint bodies MUST be extracted to dedicated modules.
- Any entrypoint file over 350 lines MUST include a decomposition task in the active plan/tasks
	before new feature logic is added to that file.
- Endpoint registration SHOULD be grouped by bounded context (Auth, Catalogo, Pedidos, Admin,
	Health) using extension methods or dedicated endpoint-mapping files.
- Branch protection and required CI checks MUST remain enabled on integration branches.
- Documentation in docs/sdd and Analise MUST be updated whenever architecture, contracts,
	security assumptions, or workflow rules change.

## Governance

This constitution is authoritative for delivery, engineering, and review decisions in this
repository. All plans, specs, tasks, pull requests, and reviews MUST explicitly validate
compliance with these principles.

Amendment process:
1. Propose the amendment in a pull request that includes rationale, impacted principles/sections,
	 and migration or adoption steps for in-flight work.
2. Obtain approval from product and technical maintainers.
3. Update dependent templates and operational documentation in the same change.

Versioning policy (Semantic Versioning):
- MAJOR: incompatible governance changes or principle removal/redefinition.
- MINOR: new principle/section or materially expanded requirements.
- PATCH: clarifications, wording improvements, or non-semantic refinements.

Compliance review expectations:
- Each feature plan MUST include a constitution gate review before design and before implementation.
- Each PR review MUST verify test evidence, tenant isolation impact, contract impact, and
	observability impact.
- Non-compliant changes MUST be blocked until corrected or until constitution amendment is approved.

**Version**: 1.0.0 | **Ratified**: 2026-04-12 | **Last Amended**: 2026-04-12
