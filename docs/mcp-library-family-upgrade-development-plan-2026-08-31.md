# MCP Library Family and Project Library Upgrade Development Plan

Status: Proposed

Date: 2026-08-31

Scope: Expose the already implemented library-family and project library-upgrade
domain workflow through SereinFlow MCP. This plan intentionally does not add a
second upgrade engine, does not call the REST API from MCP, and does not change
the immutability model for library artifacts, flow versions, or run snapshots.

Related domain plan: docs/library-version-upgrade-plan.md.

## 1. Outcome

After this work, an authenticated MCP client can:

1. Discover library families, their immutable artifacts, and a project's
   current library references.
2. Assign an artifact to an existing family or create a family while assigning
   the artifact, using the existing catalog behavior.
3. Create a project-scoped upgrade analysis for a source artifact, target
   artifact, and selected development flows.
4. Inspect compatibility issues, affected nodes, acknowledgement requirements,
   and the persisted upgrade plan.
5. Explicitly apply one or more selected flow upgrades through the established
   MCP preview, fingerprint, confirmation, idempotency, audit, and
   authorization conventions.
6. Re-read the project library references and resulting flow versions after an
   apply operation.

The feature will give an AI client the same safe conceptual workflow already
available through the Application and REST layers. It will not expose a
shortcut that simply detaches one library and attaches another.

## 2. Architecture Findings

The capability is primarily an MCP exposure gap, rather than an absent domain
feature.

    HTTP /mcp or stdio
      -> SereinFlow.Mcp protocol host and tool catalog
      -> MCP handler
      -> Application service
      -> Infrastructure persistence transaction
      -> immutable library artifacts and versioned flow definitions

### Existing domain capability

| Concern | Existing implementation and behavior |
| --- | --- |
| Library family catalog | ILibraryCatalogService.ListFamiliesAsync and AssignFamilyAsync; assignment accepts FamilyId, Name, and Description. A request without FamilyId creates a family by name. |
| Immutable artifacts | LibraryDto carries family and lifecycle data. Importing an artifact does not mutate its ZIP payload. |
| Project references | ProjectLibraryService adds/removes ProjectLibraryReferences and already prevents unsafe direct removal of a referenced artifact. |
| Upgrade preview | LibraryUpgradeService.PreviewAsync persists a LibraryUpgradePlanDto with per-flow compatibility analysis. |
| Upgrade apply | LibraryUpgradeService.ApplyAsync transforms only selected source-library nodes, creates a new development flow version, and checks the expected version. |
| Batch behavior | LibraryUpgradeService.ApplyBatchAsync invokes the single-flow apply path. Each flow has its own transaction and batch results explicitly separate successes from failures. |
| Atomic commit | IFlowLibraryUpgradeStore.CommitAsync is the per-flow persistence boundary. It saves the new flow version and target project reference, then removes the source reference only if it is no longer used. |
| REST parity | Library family and project library-upgrade controllers already expose the application services to the Web API. |
| Current MCP coverage | MCP has artifact reads, package import, project-library attachment, preview records, idempotency, audit, and authorization. It has no family reads/assignment or upgrade preview/apply tools/resources. |

### Existing MCP cross-cutting components to reuse

| Component | Required reuse |
| --- | --- |
| McpPreviewService | Store a principal-bound, fingerprinted, short-lived preview before every family assignment or upgrade apply. |
| McpIdempotencyService | Replay the result for a repeated apply request with the same idempotency key. |
| McpMutationGate | Serialize same-key local mutation attempts so idempotency is not defeated by concurrent requests in one MCP host. |
| McpToolAuditService | Record bounded audit facts for preview and apply without storing secret material or unbounded flow definitions. |
| McpToolAuthorization | Authorize project access and operation permissions both when the preview is created and when it is applied. |
| McpPreviewPayloadReaders | Extend the preview readback switch for the new preview payload types. |
| SereinFlowMcpToolCatalogFactory | Register the new tools with descriptions, schemas, and handlers. |
| McpResourceReader | Add fixed/resource-template handlers for family, project-reference, and upgrade-plan reads. |

## 3. Critical Design Decision

Do not model a library upgrade as:

    detach source artifact
      -> attach target artifact

That sequence is unsafe and is inconsistent with existing behavior:

- Direct removal is blocked when the current development flow or historical
  production/run data still uses the source artifact.
- A compatibility result must be evaluated against each selected flow.
- The transformation must update runtime metadata, stable node/parameter
  contracts, and data connection target port IDs together.
- The upgraded definition must be validated and persisted as a new development
  version. Existing definitions and run snapshots must remain immutable.
- The source reference may be removed only after the transactional commit
  proves it is no longer used by current development flows.

Therefore MCP handlers must inject and call LibraryUpgradeService directly.
They must not invoke API endpoints, read/write database tables directly, or
reimplement the compatibility analyzer or definition transformation.

## 4. Target MCP Contract

Use both tools and resources. Resources suit deterministic, cacheable reads
whose URI is already known. Named tools make the workflow discoverable to MCP
clients that rely primarily on tools/list.

### 4.1 Read tools

| Tool | Input | Result |
| --- | --- | --- |
| sereinflow_list_library_families | includeArchivedArtifacts, maxItems | Bounded LibraryFamilyDto summaries including immutable artifacts. |
| sereinflow_get_library_family | familyId | One LibraryFamilyDto or a structured not-found error. |
| sereinflow_get_project_libraries | projectId | Bounded ProjectLibraryReferenceDto list, including artifact/family/lifecycle facts needed to choose a target. |
| sereinflow_get_library_upgrade | projectId, upgradeId | Existing LibraryUpgradePlanDto, including per-flow issues and previous successful applications. |

The exact bounded result envelope should follow the existing library/project
read tools. Do not return package content, raw uploaded assemblies, or
unbounded full flow-definition JSON in these catalog reads.

### 4.2 Resources and templates

Add these discoverable URIs:

| URI | Source |
| --- | --- |
| sereinflow://library-families | ILibraryCatalogService.ListFamiliesAsync |
| sereinflow://library-families/{familyId} | Family lookup/read model |
| sereinflow://projects/{projectId}/libraries | ProjectLibraryService project references |
| sereinflow://projects/{projectId}/library-upgrades/{upgradeId} | LibraryUpgradeService.GetPlanAsync |

Use the same project-scope authorization as equivalent read tools. A resource
must never allow an unauthorised caller to bypass project access checks merely
because it knows a GUID.

### 4.3 Library-family mutation tools

    sereinflow_preview_library_family_assignment
    sereinflow_apply_library_family_assignment

Preview input should mirror AssignLibraryFamilyRequestDto:

    {
      "libraryId": "sha256 artifact id",
      "familyId": "optional existing family id",
      "name": "required when creating a family",
      "description": "optional"
    }

Apply input must use the normal MCP apply envelope:

    {
      "previewId": "guid",
      "previewFingerprint": "sha256 value",
      "confirmation": "APPLY",
      "idempotencyKey": "unique client key"
    }

The preview response must make the mutation explicit: existing family versus
new family, artifact ID, normalized family fields, and whether a previous
family relationship will change. Neither preview nor apply may alter package
import semantics: familyId and baselineArtifactId on
sereinflow_preview_library_package remain compatibility-analysis inputs only.
They must not silently persist the imported artifact's family membership.

### 4.4 Library-upgrade mutation tools

    sereinflow_preview_library_upgrade
    sereinflow_apply_library_upgrade

The preview input maps directly to LibraryUpgradePreviewRequestDto:

    {
      "projectId": "guid",
      "sourceArtifactId": "sha256 artifact id",
      "targetArtifactId": "sha256 artifact id",
      "flowIds": ["guid"]
    }

The preview should return both:

1. The persisted LibraryUpgradePlanDto, including every per-flow
   FlowLibraryUpgradePreviewDto and every compatibility issue.
2. An MCP preview descriptor with previewId, previewFingerprint, expiry, and
   the information the client must return to apply.

The apply input wraps the existing batch DTO:

    {
      "previewId": "guid",
      "previewFingerprint": "sha256 value",
      "confirmation": "APPLY",
      "idempotencyKey": "unique client key",
      "flows": [
        {
          "flowId": "guid",
          "expectedFlowVersion": 12,
          "acknowledgedItemIds": ["compatibility issue id"]
        }
      ]
    }

The preview payload must contain the persisted upgrade plan ID. The handler
must use it to call ApplyAsync for one flow or ApplyBatchAsync for many flows;
the apply request must not accept a client-selected source, target, or plan ID
outside the fingerprinted preview payload.

### 4.5 User-visible upgrade workflow

    read project library references
      -> read family/artifact candidates
      -> preview source-to-target upgrade for selected development flows
      -> inspect blockers and acknowledgement-required issues
      -> explicit user confirmation
      -> MCP apply with expected flow versions and idempotency key
      -> read project references and upgrade plan again
      -> read changed development flow versions as needed

This workflow preserves the existing distinction between analyzing an upgrade
and authorising a state change.

## 5. Authorization, Safety, and Transaction Semantics

### Authorization matrix

| Operation | Required scope |
| --- | --- |
| List/read library families | Existing catalog read visibility; archived artifacts remain subject to the current catalog policy. |
| Read project library references and upgrade plans | Project access plus library read/sensitive-read policy used by existing project library reads. |
| Preview/apply a project upgrade | Project-scoped flow.write and library.manage. The server must enforce both, not choose either one. |
| Preview/apply family assignment | Administrator access plus library.manage, because a family is global catalog metadata and can affect multiple projects. |

Add a distinct library.upgrade preview operation to
McpToolAuthorization.RequirePreviewPermission. It should enforce the composite
project permissions during preview readback and apply. The initial
implementation should not infer an upgrade permission from an unrelated
project write permission.

### Required MCP mutation invariants

1. A preview is principal-bound and expires after the existing preview TTL.
2. Apply requires the exact preview ID and fingerprint, confirmation equal to
   APPLY, and a non-empty idempotency key.
3. A different principal, missing project scope, expired preview, altered
   fingerprint, or incorrect operation fails before the domain mutation.
4. The idempotency response is written/read according to existing MCP
   conventions and audited without secrets.
5. After a durable mutation succeeds, consume the preview. A fresh preview is
   required for another family assignment or for unresolved upgrade flows.
6. Apply responses always carry per-flow successes and failures. MCP must not
   state or imply global atomicity for a batch.

### Batch and partial-success policy

The domain implementation is deliberately per-flow transactional, not
all-or-nothing across the entire selected flow list. The MCP contract must
preserve that policy:

- One flow's version conflict, missing acknowledgement, or validation failure
  does not roll back successfully committed sibling flows.
- The apply response exposes succeeded and failed arrays with flow IDs,
  status/code/message, and current version where available.
- When any flow has been durably changed, the original MCP preview is consumed.
  Retrying remaining flows requires a fresh preview so the client sees current
  project references and new development versions.
- The operation description, guidance, and tests must call this out to prevent
  a client from interpreting HTTP/MCP success as full-batch success.

## 6. Application Boundary Hardening Before Exposure

LibraryUpgradeService currently validates family membership, target lifecycle
at apply, compatibility, acknowledgement, and expected flow version. Before
MCP exposes this workflow, strengthen the Application boundary so REST and MCP
have the same safe behavior.

### Preview preconditions

1. Reject a missing or archived project.
2. Reject an unavailable or archived target artifact before creating a plan.
3. Require the source artifact to be currently referenced by the selected
   project.
4. Require every selected flow to actually contain one or more nodes using the
   source artifact. Return a structured error for non-using flows rather than
   creating a misleading zero-effect plan.
5. Retain the existing same-family and non-empty-flow validation.

### Apply-time rechecks

1. Re-read the project and reject an archived project.
2. Re-read source and target lifecycle and project-reference state.
3. Verify the source remains a project reference and the target remains
   available.
4. Verify the selected flow still uses the source artifact before
   transformation; the expected version check alone does not cover all
   reference/lifecycle changes.
5. Retain existing compatibility re-analysis, blocking-issue, acknowledgement,
   flow-definition validation, normalization, and commit checks.

These checks prevent a saved plan from becoming an indirect arbitrary
artifact-attachment mechanism after catalog or project state changes. They
also ensure that the API and MCP adapters expose a single domain rule set.

## 7. Implementation Plan

### Phase 0: Define the contract and harden the domain boundary

Files expected to change:

- src/SereinFlow.Application/LibraryUpgradeService.cs
- src/SereinFlow.Application/ProjectLibraryService.cs, only if a reusable
  project-reference query is needed
- src/SereinFlow.Application/Persistence interfaces, only if the current
  stores cannot answer lifecycle/reference/use checks efficiently
- src/SereinFlow.Contracts/Dtos.cs, only for a precise structured failure/read
  model that cannot be represented by existing DTOs
- tests/SereinFlow.Application.Tests

Work:

1. Add the preview and apply preconditions from section 6 at the Application
   boundary.
2. Prefer existing repositories/stores and small query methods over scanning
   raw persisted flow JSON in MCP.
3. Keep existing REST request/response DTOs. Do not create a REST-shaped MCP
   duplicate DTO unless the MCP envelope genuinely needs extra fields.
4. Add tests that prove an unsafe plan cannot be created or applied.

Exit criteria:

- REST and a future MCP handler receive identical errors for invalid project,
  lifecycle, reference, and selected-flow state.
- The per-flow commit transaction remains the only path that changes a
  development definition and project library references.

### Phase 1: Read-model exposure

Files expected to change:

- src/SereinFlow.Mcp/Tools/McpLibraryToolHandlers.cs
- src/SereinFlow.Mcp/Tools/McpReadModelToolHandlers.cs, if that is the current
  home for bounded read tools
- src/SereinFlow.Mcp/Tools/SereinFlowMcpToolCatalogFactory.cs
- src/SereinFlow.Mcp/McpResourceReader.cs
- src/SereinFlow.Mcp/SereinFlowMcpBackend.cs, if the resource dispatcher needs
  an additional registration
- tests/SereinFlow.Mcp.Tests

Work:

1. Inject ILibraryCatalogService, ProjectLibraryService or its read
   abstraction, and LibraryUpgradeService into the appropriate read handlers.
2. Add the four read tools and four resource URIs/templates in section 4.
3. Apply bounded list sizes and existing authorization checks.
4. Return existing DTOs or intentionally bounded projections, not storage
   entities or package contents.

Exit criteria:

- MCP tools/list and resources/list/resources/templates/list advertise the new
  contract.
- Resource and tool reads have consistent results and project access failures.

### Phase 2: Family assignment preview and apply

Files expected to change:

- src/SereinFlow.Mcp/Tools/McpLibraryToolHandlers.cs
- src/SereinFlow.Mcp/Tools/McpToolAuthorization.cs
- src/SereinFlow.Mcp/Tools/McpPreviewPayloadReaders.cs or its equivalent
- src/SereinFlow.Mcp/Tools/SereinFlowMcpToolCatalogFactory.cs
- tests/SereinFlow.Mcp.Tests

Work:

1. Add a preview payload type that contains the artifact ID and the complete
   AssignLibraryFamilyRequestDto after validation/normalization.
2. Preview the existing/new family outcome without persisting it.
3. On apply, run the standard preview ownership/fingerprint/confirmation
   checks, then call ILibraryCatalogService.AssignFamilyAsync exactly once
   through the idempotency/mutation gate.
4. Add administrator plus library.manage authorization and audit facts.
5. Consume the preview after a successful durable assignment.

Exit criteria:

- An MCP client can assign an artifact to an existing family or create a new
  family, but cannot do so with only project scope.
- Repeating the same apply key is replayed rather than creating a second
  mutation.

### Phase 3: Upgrade preview and apply

Files expected to change:

- src/SereinFlow.Mcp/Tools/McpLibraryToolHandlers.cs
- src/SereinFlow.Mcp/Tools/McpToolAuthorization.cs
- src/SereinFlow.Mcp/Tools/McpPreviewPayloadReaders.cs or its equivalent
- src/SereinFlow.Mcp/Tools/SereinFlowMcpToolCatalogFactory.cs
- tests/SereinFlow.Mcp.Tests

Work:

1. Add preview_library_upgrade. Authorize project flow.write plus
   library.manage, call LibraryUpgradeService.PreviewAsync, and save the plan
   ID with an MCP preview descriptor/payload.
2. Add apply_library_upgrade. Validate the MCP apply envelope, load the
   preview, require the composite permissions again, and call the application
   apply service using only preview-bound project/plan data.
3. Support one-flow and multi-flow requests by selecting ApplyAsync or
   ApplyBatchAsync. Preserve the application response shape for failures.
4. Audit source artifact, target artifact, plan ID, selected-flow count,
   succeeded-flow count, and failed-flow count. Do not audit full definitions,
   package data, or raw literal values.
5. Consume the preview after the first durable application and return an
   explicit fresh-preview requirement for pending flows.

Exit criteria:

- The MCP handler never performs a detach/attach substitution.
- An upgraded flow creates a newer development definition and preserves prior
  flow versions and run snapshots.
- Batch success/failure behavior is explicit and independently retryable after
  a fresh preview.

### Phase 4: Guidance, prompt, and integration parity

Files expected to change:

- src/SereinFlow.Api/mcp/sereinflow-ai-guide.md
- src/SereinFlow.Api/mcp/sereinflow-library-package-skill.md
- src/SereinFlow.Mcp/McpPromptCatalog.cs
- plugins/sereinflow-ai-toolkit/skills/sereinflow/SKILL.md
- tests/SereinFlow.Mcp.Tests/McpAiGuidanceTests.cs
- tests/SereinFlow.Api.IntegrationTests

Work:

1. Document the read -> preview -> inspect -> confirm -> apply -> reread
   workflow in the runtime-hosted guidance.
2. Add a sereinflow.upgrade-library prompt that guides analysis and
   acknowledgement inspection. It must not imply that a preview is
   authorization to apply.
3. Update source-plugin capability wording so clients discover the feature
   after a server update, without copying the entire server-side workflow into
   the plugin.
4. Add an HTTP MCP discovery and end-to-end smoke test; add stdio parity only
   where the existing test harness makes it low-cost.

Exit criteria:

- An agent reading the live guidance chooses a family-aware upgrade flow rather
  than package import plus direct attachment.
- HTTP and stdio expose the same tool names and schemas from the common MCP
  catalog.

## 8. File-Level Ownership Map

| Responsibility | Primary location |
| --- | --- |
| Family contracts and upgrade DTOs | src/SereinFlow.Contracts/Dtos.cs |
| Catalog family list/assignment | src/SereinFlow.Application/LibraryCatalog.cs and Infrastructure catalog persistence |
| Domain upgrade orchestration | src/SereinFlow.Application/LibraryUpgradeService.cs |
| Project reference policy | src/SereinFlow.Application/ProjectLibraryService.cs |
| Per-flow atomic upgrade commit | src/SereinFlow.Infrastructure/Persistence/FlowLibraryUpgradePersistence.cs |
| REST parity reference only | src/SereinFlow.Api/Controllers/ProjectLibrariesController.cs and LibrariesController.cs |
| MCP library adapter | src/SereinFlow.Mcp/Tools/McpLibraryToolHandlers.cs |
| MCP catalog registration | src/SereinFlow.Mcp/Tools/SereinFlowMcpToolCatalogFactory.cs |
| MCP resources | src/SereinFlow.Mcp/McpResourceReader.cs |
| MCP mutation security | src/SereinFlow.Mcp/Tools/McpToolAuthorization.cs, McpPreviewService, McpIdempotencyService, McpMutationGate, and McpToolAuditService |
| Agent guidance | src/SereinFlow.Api/mcp/sereinflow-ai-guide.md and sereinflow-library-package-skill.md |

## 9. Test Matrix

### Application tests

Add focused LibraryUpgradeService tests for:

1. Missing or archived project.
2. Source/target artifact missing, unassigned, different-family, unavailable,
   or archived target.
3. Source artifact not referenced by the project.
4. Selected flow absent from project or not using the source artifact.
5. Same-family valid preview persists a plan with compatibility findings.
6. Apply rechecks lifecycle/reference state after preview.
7. Blocking issue, missing acknowledgement, flow-version conflict, and invalid
   transformed definition leave data unchanged.
8. Successful apply creates only the next development version and updates
   project references through the existing commit behavior.
9. Batch with one conflict returns one success and one failure without
   rollbacking the successful flow.

### MCP unit/integration tests

Add tests for:

1. tools/list, resources/list, resource templates, and prompts list advertise
   the new features.
2. Family and project-reference reads respect catalog/project access and list
   bounds.
3. Family assignment requires administrator and library.manage permissions.
4. Upgrade preview and apply require both flow.write and library.manage.
5. Preview ownership, altered fingerprint, expired preview, wrong operation,
   and changed principal all fail before mutation.
6. Idempotency replays family assignment and upgrade apply responses.
7. Upgrade happy path returns plan details, applies a new flow version, and
   allows post-apply readback.
8. Same-family errors, archived target, source-not-referenced, selected
   non-using flow, missing acknowledgement, and expected-version conflict
   propagate as structured failures.
9. Partial batch result is explicit, preview consumption is correct after
   durable work, and a fresh preview is required for unresolved flows.
10. Audit records are bounded and omit package/body secrets.

### API/MCP boundary tests

1. Compare REST and direct application behavior for the new preconditions.
2. Execute an HTTP MCP discovery plus project upgrade smoke workflow.
3. When applicable, run the same catalog/handler schema test through stdio to
   ensure the transport did not alter the contract.

## 10. Verification

Run the targeted test projects first, then the affected solution tests:

    dotnet test tests/SereinFlow.Application.Tests/SereinFlow.Application.Tests.csproj
    dotnet test tests/SereinFlow.Mcp.Tests/SereinFlow.Mcp.Tests.csproj
    dotnet test tests/SereinFlow.Api.IntegrationTests/SereinFlow.Api.IntegrationTests.csproj

Run the existing formatting/build commands configured by the repository and:

    git diff --check
    git -c safe.directory=D:/Project/dotnet/SereinFlow status --short

Manual protocol verification:

1. Initialize an authenticated MCP client.
2. Discover tools, resources, resource templates, and prompts.
3. Read a project library list and an assigned family.
4. Preview a same-family upgrade that includes an acknowledgement-required
   compatibility issue.
5. Confirm apply with the expected flow version and acknowledgement.
6. Re-read the upgrade plan, project library references, and development flow.
7. Repeat the apply with the same idempotency key and verify response replay.
8. Attempt a different-principal, stale-fingerprint, and missing-permission
   apply and verify no durable change occurs.

## 11. Non-Goals

- Replacing the compatibility analyzer or manually editing flow JSON in an MCP
  handler.
- Introducing cross-flow global transactions; the current explicit
  per-flow-transaction behavior is retained.
- Treating package import metadata as an implicit family assignment.
- Allowing upgrade to cross family boundaries.
- Deleting or overwriting immutable library ZIP artifacts, old flow versions,
  production definitions, or run snapshots.
- Exposing raw library packages, arbitrary DLL data, or sensitive flow
  literals in family and upgrade catalog reads.

## 12. Definition of Done

The work is complete when:

1. The live MCP catalog advertises family reads/assignment and project upgrade
   reads/preview/apply through documented tools and resources.
2. MCP calls the established Application services directly and no MCP-specific
   data access or upgrade transformation exists.
3. Domain preconditions prevent archived, unreferenced, unavailable, and
   non-using-flow upgrades equally for REST and MCP callers.
4. Every mutation follows preview, fingerprint, confirmation, idempotency,
   authorization, audit, and post-apply readback conventions.
5. The contract explicitly reports per-flow batch outcomes and requires a
   fresh preview after any durable partial or full application.
6. Automated tests cover authorization, compatibility, concurrency,
   idempotency, expiry, partial batches, and immutable-history guarantees.
