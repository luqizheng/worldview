# World View

World View is a local-first desktop application for creating, editing, reconstructing, and presenting fictional or historical worlds and their maps.

This repository contains the project documentation baseline and the existing domain-design record.

## Documentation

Start here: [Documentation Index](docs/README.md) · [Migration Manifest](docs/MIGRATION-MANIFEST.md)

- [Core Domain Architecture](docs/architecture/core-domain/)
- [Map Editor](docs/map-editor/)
- [Map Generation](docs/map-generation/)
- [Map Engine and Rendering](docs/map-engine/)
- [AI Assistant](docs/ai-assistant/)
- [Architecture Decision Records](docs/adr/)
- [P2-01-D01 Domain Discussion](docs/P2-01-D01/)

## Current product and technical direction

- Desktop application: C# + Avalonia.
- Local-first, single-user / small-team operating model.
- Core Domain owns formal map data, commands, and history semantics.
- AI Assistant proposes and stages changes; formal writes pass through validation, authorization, and Core Domain services.
- Map rendering, geospatial computation, and renderer technology remain separately evaluated; no final engine selection is implied by this documentation.

## Document status

Documents explicitly marked Accepted/Frozen are binding within their scope. Proposals, reviews, discussions, work plans, and test specifications are not implementation evidence or automatic approvals. Historical versions are retained for traceability.

## Contributions

Please preserve document status labels, version history, and the distinction between confirmed decisions and recommendations when changing project documentation.
