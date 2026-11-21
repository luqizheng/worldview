# World View — ME-01-02 World Knowledge Model & Story-to-Spatial Bridge

- Version: 0.1
- Date: 2026-11-21
- Status: Design Working Baseline — discussion-complete for conceptual architecture; detailed schema and implementation contracts remain open
- Scope: World Knowledge, story understanding, spatial interpretation, and the boundary between narrative knowledge and formal map data
- Product constraints: single-user, local-first; C# + Avalonia desktop direction; one built-in AI Assistant; Core Domain remains authoritative for formal map data

## 1. Purpose

World View must allow a creator to begin with an incomplete story premise and progressively develop a coherent world. The creator should not need to prepare a complete world bible or understand GIS terminology before starting.

The system therefore needs to preserve two related but distinct kinds of information:

1. **World Knowledge** — what the creator says or intends about the fictional world: characters, species, cultures, groups, places, events, historical facts, and their relationships.
2. **Map Domain Data** — formal spatial objects and their geometry, location, attributes, spatial relationships, and historical state.

A controlled **Story-to-Spatial Bridge** interprets which parts of World Knowledge have spatial meaning and translates them into spatial constraints, object candidates, relationships, or historical operations. Conversation text is not itself map data.

## 2. Architectural principles

### 2.1 Separate narrative meaning from spatial representation

A story entity does not automatically become a map object. Mentioning a protagonist does not create a map marker; describing a war does not automatically create a battlefield; stating that a species prefers wetlands does not establish its actual territory.

The system must first understand meaning, provenance, certainty, time, and spatial implications. Only then may it propose spatial representations.

### 2.2 Preserve creator intent

Creator-authored facts and intent must not be silently rewritten by AI inference. AI may propose explanations and alternatives, but must preserve the distinction between confirmed constraints, preferences, derived implications, and unresolved questions.

### 2.3 Formal data authority remains in Core Domain

World Knowledge is not a shadow copy of Core Domain Object, Geometry, History, or World State. Formal map objects, stable Object identities, geometry, spatial relationships, Commands, and historical state remain Core Domain responsibilities. AI Assistant owns task execution and candidate/proposal/authorization evidence, and invokes Domain Services for formal changes.

### 2.4 Keep V1 deliberately lightweight

Use a small domain model of entities, assertions, relations, evidence, and spatial interpretations. Do not introduce a general-purpose graph database, ontology engine, rule engine, or independent event-sourcing system for V1.

## 3. World Knowledge Model

### 3.1 Knowledge Entity

A Knowledge Entity represents a stable narrative/world concept, for example:

- Character / Person
- Species / Population
- Culture / Society
- Faction / Polity
- Place / Region (as a narrative concept; not necessarily a formal map object)
- Historical Event
- Organization / Group
- Natural or supernatural phenomenon

Entity identity is independent of the wording used in a conversation. Renaming or refining a description must not accidentally create a second entity.

### 3.2 Knowledge Assertion

A Knowledge Assertion records one claim about an entity or relation. Assertions are atomic enough to be independently sourced, confirmed, revised, or contradicted.

Examples:

- A species prefers warm, humid environments.
- A character was born in a coastal city.
- A polity controlled a region during a historical interval.
- A migration occurred from a northern region toward a southern region.

Do not store an entire paragraph as one indivisible assertion when it contains multiple facts with different certainty or provenance.

### 3.3 Knowledge Relation

A Knowledge Relation connects entities with a typed semantic relationship, such as:

- character belongs to species or culture;
- group migrates between regions;
- faction governs or contests a territory;
- event involves characters and affects places;
- place is culturally significant to a group.

Relations may have their own assertions, evidence, confidence, and historical validity. A relation is not automatically a geometric topology relation.

### 3.4 Evidence and provenance

Each assertion or relation must retain its origin. Initial source categories:

- `CreatorStatement` — explicit creator statement;
- `CreatorIntent` — desired narrative or world-building effect;
- `DocumentSource` — an existing project/world document;
- `ExternalReference` — external factual reference, when intentionally used;
- `AIInference` — model-generated inference;
- `SystemDerived` — deterministic computation or rule-based derivation.

Evidence should reference the source conversation turn, document, or derivation record where available. A summary must not replace the original evidence reference.

### 3.5 Knowledge status and source type are independent

Use separate dimensions:

**Status**
- `Confirmed` — accepted by the creator or authoritative project source;
- `Inferred` — derived by AI or system, not yet confirmed as a world fact;
- `Pending` — insufficient information or awaiting a creator decision;
- `Contradicted` — conflicts with another assertion or authoritative constraint.

**Source type** is the provenance category above. For example, an AI inference can later be confirmed by the creator without erasing that it originated as an inference.

Do not assign one status to an entire entity. Different assertions about the same entity may have different statuses.

### 3.6 Historical validity

World Knowledge may describe changing relationships. Distinguish:

- `valid_from` / `valid_to`: when the assertion is true in the fictional world's historical time;
- `recorded_at`: when World View recorded it;
- `source/evidence`: why the system believes it;
- status: whether it is confirmed, inferred, pending, or contradicted.

V1 should support historical intervals where needed without introducing a general temporal graph framework. Canonical historical time semantics remain owned by Core Domain.

## 4. Story-to-Spatial Bridge

### 4.1 Responsibility

The bridge interprets World Knowledge and produces explicit spatial intents. It must not directly create or mutate formal Core Domain data.

### 4.2 Spatial interpretation categories

Classify story information into one or more of these categories:

1. `NonSpatial` — no direct map implication, e.g. personality or beliefs.
2. `SpatialContext` — association with a place or region, e.g. birthplace or residence.
3. `SpatialConstraint` — conditions that should constrain placement or generation.
4. `ObjectCandidate` — a possible spatial object such as a mountain range, river, settlement, or road.
5. `SpatialRelation` — semantic relation such as blocks, borders, connects, flows through, surrounds, or is adjacent to.
6. `SpatiotemporalEvent` — migration, travel, war, expansion, retreat, or territorial change with spatial and temporal implications.

Classification may remain pending or have multiple plausible interpretations. The bridge must preserve ambiguity rather than force a premature map representation.

### 4.3 Spatial Intent record

A Spatial Intent is a proposed interpretation with at least:

- source Knowledge Entity / Assertion / Relation references;
- intent category and semantic type;
- target spatial scope (world, region, locality, site, or unresolved);
- constraints and preferences;
- temporal scope where relevant;
- certainty/status and rationale;
- dependencies on other knowledge or spatial facts;
- suggested operations or candidate types;
- affected existing objects/regions, when known.

Spatial Intent is an interpretation/proposal, not a formal map object and not a Core Domain Command.

### 4.4 Hard, soft, and derived constraints

- `HardConstraint`: an explicit confirmed condition that must not be violated without creator-authorized change.
- `SoftConstraint`: a preference or tendency used to rank/recommend alternatives; it may be traded off with explanation.
- `DerivedConstraint`: a consequence inferred from confirmed data or deterministic domain rules, such as drainage following terrain gradients.

Do not silently promote a soft preference into a hard constraint. Do not present an AI inference as creator-confirmed fact.

### 4.5 Spatial interpretation examples

| Narrative input | Knowledge result | Spatial interpretation |
|---|---|---|
| “The protagonist is a prince.” | Character and social status | NonSpatial unless a place or spatial role is stated |
| “The protagonist was born in the capital.” | Character-place relation | SpatialContext; link to an existing or unresolved place concept |
| “Two kingdoms went to war.” | Historical event and participants | SpatiotemporalEvent; ask/derive affected areas only when needed |
| “This species prefers humid forests.” | Species habitat preference | SoftConstraint for habitat suitability, not actual territory |
| “The army marched south along the river.” | Movement event, direction, river relation | Candidate route and time-bounded movement, subject to geographic validation |
| “The mountain blocks northern invaders.” | Creator intent and defensive relation | Mountain ObjectCandidate plus a defensive SpatialRelation; geometry remains to be determined |

## 5. Conflict and uncertainty handling

1. Preserve conflicting assertions and their evidence; do not overwrite one with another.
2. Determine whether the conflict is real, temporal (different historical periods), perspectival (different narrators), or merely ambiguous wording.
3. If the conflict changes important spatial structure or creator intent, request clarification.
4. If it is low-impact and can be represented as unresolved alternatives, continue unrelated work.
5. Confirmed creator constraints take precedence over AI inference. A later creator change must be explicit and traceable.
6. Never use confidence scores as a substitute for provenance or creator confirmation.

## 6. Relationship to Guided Initialization

Guided Initialization consumes World Knowledge and Spatial Intents. It may request clarifications, generate spatial candidates, validate them, and prepare a reviewable proposal. It does not own a separate formal map history or bypass Core Domain.

The interaction can continue while the map progressively matures. The creator may generate an initial world skeleton before all knowledge is complete, then enrich regions and revise interpretations incrementally.

## 7. Relationship to existing architecture

- The built-in AI Assistant is the single user-facing assistant; internal logical modules do not imply separate agents or processes.
- Task Planning & Execution Engine owns Task/Plan/Step execution and recovery.
- Candidate, Validation, Selection, Change Set, authorization, and submission evidence reuse the existing AI Assistant architecture.
- Core Domain remains authoritative for formal Map/Object identity, geometry, spatial relationships, history, and reconstructed World State.
- LLM does not access or write database tables directly. Domain Services validate and apply formal operations.
- Map presentation (natural geography, fantasy cartography, labels, symbols) is not the authority for spatial facts.

## 8. V1 scope

**In scope**
- stable Knowledge Entity identity;
- atomic Knowledge Assertions and typed Relations;
- provenance and independent status dimensions;
- basic historical validity;
- spatial interpretation categories;
- Hard / Soft / Derived constraints;
- Spatial Intent records with source references and dependencies;
- conflict preservation and clarification triggers.

**Out of scope for V1**
- general-purpose ontology editor;
- arbitrary graph query language;
- autonomous world canon rewriting;
- automatic conversion of every narrative entity into map objects;
- automatic resolution of creator contradictions;
- a second spatial/history authority outside Core Domain.

## 9. Acceptance criteria

1. A creator can describe an incomplete story and the system can extract entities, assertions, and relations without requiring a pre-authored world bible.
2. Each extracted assertion retains provenance and an independent status.
3. Creator intent, confirmed fact, AI inference, and unresolved information remain distinguishable.
4. The system can represent a species' habitat preference without asserting its actual distribution.
5. A narrative event can be spatially interpreted without automatically creating geometry or formal map objects.
6. Spatial intents can be traced back to their source knowledge.
7. Conflicts are preserved and surfaced rather than silently overwritten.
8. Formal map changes remain behind Core Domain Domain Services and the existing proposal/application boundary.

## 10. Open implementation decisions

The conceptual model is sufficiently discussed for this working baseline. The following remain intentionally open for later design:

- physical persistence mapping and whether World Knowledge shares the project database file while retaining module ownership;
- exact entity/assertion/relation identifiers and indexing strategy;
- evidence retention and privacy/retention policy;
- exact serialization of geometry-bearing spatial intents;
- integration with Core Domain Object references and historical time contracts;
- detailed AI extraction evaluation and correction UX.

These are not blockers to proceeding with ME-01 conceptual architecture, but they are blockers to final physical schema and implementation handoff.