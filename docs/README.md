# World View Project Documentation

This repository is the version-controlled documentation baseline for the World View project.

## Structure

- `architecture/`: product and technical architecture, domain model, persistence, and ADRs.
- `map-editor/`: desktop map editor architecture and interaction contracts.
- `ai-assistant/`: AI Assistant planning, execution, provider, and integration contracts.
- `history/`: superseded or historical source documents retained for traceability.
- `P2-01-D01/`: historical-state and command-domain design discussion package (existing).

## Authority and status

- Accepted/Frozen ADRs define binding domain semantics unless explicitly amended.
- Documents labelled Proposal, Discussion, Draft, or Review are not frozen merely because they are stored here.
- Newer documents supersede older versions only where their scope explicitly says so; historical files are retained rather than silently overwritten.
- The Core Domain owns formal map data and history. AI Assistant proposals are staged and must pass validation/authorization and Domain Services before formal application.
- This repository is a documentation and design source of truth; it does not imply implementation completion or provider-test completion.

## Current baseline

The current baseline index is maintained alongside the latest README and work-plan documents. See the latest versioned README in `architecture/` and the domain-specific documents linked from this tree.

## Design principles

- Local-first, single-user/small-team, low-concurrency design.
- Prefer simple, explicit, maintainable components over distributed or enterprise-scale infrastructure.
- Avalonia + C# is the selected desktop application direction; renderer and geospatial technology decisions remain separately evaluated.
- Preserve the distinction between confirmed decisions, recommendations, proposals, and validation gates.
