# World View — ME-05-06 Cross-architecture Integration Review & V1 Gates

- Version: 0.5
- Date: 2026-11-21
- Status: Cross-architecture design review baseline; design alignment recorded, implementation and platform proof remain open
- Scope: ME-05-01 through ME-05-08, Core Domain, Map Engine, ME-01 shared editor, ME-02 natural geography and future ME-03/ME-04 specialist consumers

## 1. Executive disposition

ME-05 establishes a coherent shared geometry and spatial-services boundary when read together with the current Core Domain and Map Engine contracts:

1. **Core Domain owns formal spatial facts.** It owns Object/Component identity, typed geometry references, Field Dataset/Revision authority, formal relationships, write-context routing and history semantics.
2. **Map Engine owns deterministic spatial computation and presentation-side acceleration.** It owns coordinate transforms, spatial predicates, measurement execution, field sampling, spatial indexes, hit-test/snap evaluation and derived representations. It does not publish formal domain facts.
3. **Editor owns creator interaction and review.** It owns tools, temporary geometry, staging, previews, context selection and diagnostics presentation. It cannot bypass Domain Services.
4. **AI Assistant proposes and explains.** It may interpret intent, plan analyses, produce Candidates and explain validation outcomes. It does not directly write Core Domain or invent numeric spatial results.
5. **Formal writes are typed and context-specific.** Map Initial State, post-zero Map/Branch History, Project-level Shared Object definition and Map Source Field Baseline are distinct authority paths.
6. **No universal topology database or general GIS clone is required for V1.** Reusable predicates plus specialist domain validation are sufficient for the initial product scope.

The ME-05 documents are recommended working contracts. They do not imply that all descriptors, serializers, algorithms, physical schemas or runtime tests have been implemented or frozen.

## 2. Cross-architecture ownership matrix

| Concern | Formal authority | Execution / projection | Must not own |
|---|---|---|---|
| Semantic Object identity and lifecycle | Core Domain | Editor reads resolved projection | Map Engine / AI |
| Geometry Component identity, role and membership | Core Domain typed payload | Map Engine decodes geometry | Renderer node/index |
| Geometry payload bytes | Core Asset Store, immutable | Map Engine reads and derives | Mutable renderer buffers |
| Field Dataset / Revision / current pointer | Core Domain | Map Engine reads, samples and calculates | Branch-local history or renderer |
| Formal relationship identity and temporal scope | Core Domain typed Relationship descriptor/payload | Spatial predicates may validate preconditions | Coordinate coincidence |
| Spatial predicate and measurement result | Derived query result | Map Engine / Spatial Services | Formal world fact |
| Topology/domain constraints | Specialist Core Domain validation contract | Spatial Services supplies predicate evidence | Generic universal topology graph |
| Projection and coordinate transform | Immutable spatial-reference definition and domain interpretation | Map Engine transform service | Viewport camera |
| Hit-test, snap, index and scene generation | No formal write authority | Map Engine / presentation | Core Domain authority |
| Candidate and staging | AI Assistant / Editor staging | AI plans; Editor previews | Formal Core Domain state |
| Derived calculations | Calculation subsystem | Deterministic calculation services | Core Asset ownership unless explicitly adopted |

## 3. Formal write-path reconciliation

The four paths must remain separate in API, UI language, validation and transaction handling.

| Path | What it changes | Scope / temporal effect | Key guard |
|---|---|---|---|
| Project-level Shared Object definition | Current shared base definition, including base geometry where permitted | Retroactive current definition for all referencing Maps | Expected definition revision/fingerprint |
| Map Initial State | Time-zero Map Object membership, geometry and relationships | One Map baseline | Expected baseline revision and Object Type validation |
| Post-zero Map/Branch Command | Runtime geometry or relationships at a historical point | Selected Map/Branch History only; fork boundary applies | Descriptor, expected source state and History context |
| Map Source Field Baseline | Current source physical field revision | All Branches and Historical Times of owning Map, retroactive | Expected Field Revision compare-and-swap |

Important non-equivalences:
- A Field Revision is not a historical terrain-change Event.
- A Map/Branch geometry Command on a Shared Object does not mutate its Project-level base definition or another Map's runtime state.
- A Project-level Shared Object definition edit is not a Map History Command.
- Derived geometry, a spatial query result or a Candidate is not a formal State Change.
- Renderer and viewport changes are not world-history edits.

These rules reconcile ME-05-01 with P2-09-16 Shared Definition / Checkpoint Coherence, P2-09-19 Geometry Component Identity, P2-09-20 Relationship Temporal Contract and P2-09-15 Command Catalog.

## 4. Geometry and spatial operation disposition

### 4.1 V1 formal geometry operations

The minimum formal post-zero geometry operations remain the registered Core Domain descriptor set:
- `core.geometry.adjust_component`;
- `core.geometry.refine_component`;
- `core.geometry.add_component`;
- `core.geometry.remove_component`.

Geometry split/merge remain deferred. They must not be simulated by chaining add/remove because doing so loses explicit source/result component lineage and topology semantics. Semantic Object split/merge are separate domain operations and are not implied by geometry decomposition.

### 4.2 Spatial predicates

ME-05-03 defines the reusable predicate vocabulary and outcomes. The service must distinguish:
- `True` / `False` / `Indeterminate`;
- `InvalidInput`, `Unsupported`, `OutsideDomain`, `Cancelled` and `StaleSnapshot`.

A proximity threshold is not a fuzzy intersection test. Hit-test and snap radii are screen-space interaction parameters, not formal geometry tolerances. No predicate silently moves or normalizes source coordinates.

### 4.3 Topology and relationships

P2-09-20 requires typed Relationship identity, direction, cardinality, temporal class, endpoint types, lifecycle and declared topology role. ME-05 supplies geometric evidence; it does not invent specialist semantics.

V1 does not require a universal persistent topology graph. Network connectivity is explicit domain semantics. If a specialist needs typed nodes/endpoints, it must define them in that specialist contract and its Object/Relationship descriptors.

## 5. Coordinate, precision and measurement reconciliation

- P2-09-25's recommended World View geometry JSON envelope is consistent with ME-05-02/04: explicit spatial-reference ID, units, axis order, dimension, geometry family and precision profile.
- A spatial reference is immutable once referenced. Definition changes create a new identity/version.
- Formal conversion publishes new immutable geometry Assets; query-time transforms do not rewrite source Assets.
- Domain, Map, Viewport and Device coordinates are distinct types.
- LocalCartesian is first-class for fictional maps; raw longitude/latitude Euclidean measurement is prohibited.
- Precision profile, domain tolerance, numerical robustness guard, render approximation, hit-test, snap and measurement error are distinct.
- Bézier remains formal geometry; tessellation is derived.
- Length/area/distance results must carry model, units, source fingerprints and accuracy/error metadata. Unsupported geographic/curve measurement is reported, not guessed.

No global decimal count, projection library, geometry library or geodesic implementation is frozen by ME-05.

## 6. Asset and persistence reconciliation

### 6.1 Core Asset versus Derived Artifact versus presentation cache

- **Core Asset:** immutable bytes formally referenced by a typed Core Domain owner link.
- **Derived Artifact:** calculation-owned output with provenance/freshness; not a Core Asset merely because it contains geometry or raster bytes.
- **Presentation cache:** projected paths, tessellation, spatial indexes, SceneSnapshots, GPU buffers and hit-test data; disposable and rebuildable.

A Derived Artifact becomes a formal source only after explicit adoption of its exact bytes through the appropriate Core Domain publication service.

### 6.2 Typed ownership

- Geometry Asset references use existing typed owner links for Shared Object definitions, Map Initial State or Commands.
- Field Revision uses its dedicated typed Field Revision → Core Asset ownership.
- No polymorphic generic owner table or second component registry is added for V1.
- Content deduplication may reuse identical immutable bytes, but never merges Component, Field Dataset or Field Revision identities.

### 6.3 Publication and recovery

The publication commit point is the successful Core Domain metadata transaction with verified typed references, not the existence of a file/blob.

All publication flows stage, validate, write and read-back verify bytes, then commit typed metadata with the relevant CAS/fingerprint check. If the metadata transaction fails after byte storage, the bytes remain unreferenced and are safely reconciled. Retention/GC checks all typed references, Checkpoints, audit and retained lineage before deleting anything.

ME-05-05 V0.3 integrates the logical package contract. ME-05-07 V0.5 now defines the seekable package/tile model, deterministic tile addressing, random reads, streaming edits, halo semantics and cache boundaries. ME-05-08 V0.3 now specifies the byte-level 72-byte header, 16-byte directory header and 72-byte tile entries; P2-09-33 provides 28 design vectors. ME-05-09 adds the canonical manifest schema and golden byte fixtures; ME-05-10 defines the .NET Parser/Writer conformance contract; ME-05-11 provides the S0–S10 developer handoff. P2-09-34/35/36 provide the canonical manifest schema, 28 manifest validation vectors and 41 Parser/Writer conformance vectors respectively. All are design/fixture artifacts until executed by production code. Codec, tile-size and precision selection remain open technical gates.

## 7. Map Engine and Editor integration

The Map Engine contract already defines:
- immutable scene generations and source fingerprints;
- typed coordinate pipeline and projection boundary;
- broad-phase spatial index plus narrow-phase evaluation;
- logical-pixel hit-test/snap tolerances;
- Bézier-preserving formal-to-presentation conversion;
- query cancellation and stale-result rejection.

ME-05 must consume these primitives, not create another interaction subsystem.

Before a formal write:
1. Editor identifies the formal target context.
2. Map Engine returns query/interaction evidence bound to a scene generation.
3. Editor/AI presents the candidate and diagnostics.
4. Core Domain re-resolves Object/Component/Relationship references.
5. Core Domain validates current fingerprints, descriptor, history/fork scope and authorization.
6. Core Domain commits the formal typed operation.
7. Map Engine invalidates/rebuilds only affected derived scene/query data.

A stale hit-test or snap result is never a valid formal write reference.

## 8. Specialist integration requirements

| Specialist | Shared ME-05 service consumed | Specialist-owned rules |
|---|---|---|
| ME-02 Terrain & Natural Geography | Field reads/sampling, polygon/line predicates, containment, distance/area, geometry validity | Elevation/land-water semantics, drainage, River/Lake network, terrain/climate/ecology rules |
| ME-03 Settlements & Urban Geography | Point-in-area, polygon adjacency candidates, line intersections, proximity and measurement | Settlement placement, administrative/land-use relations, road junction and routing semantics |
| ME-04 Buildings & Interior Spaces | Footprint containment, local coordinate context, entrance/access proximity and local geometry validity | Site/building association, floors/rooms, vertical and underground connectivity |
| Guided Initialization / AI | Query/validation selection and Candidate explanation | Story interpretation, creator intent, provenance, uncertainty and authorization |

A specialist must not redefine shared predicate semantics locally. It may add stricter domain validation and typed relationships.

## 9. Findings and dispositions

| ID | Finding | Severity | Disposition |
|---|---|---|---|
| M5-F01 | Core Domain and Map Engine could both be mistaken as geometry authority | P0 | Closed at design level: Core Domain owns formal identity/state; Map Engine computes and presents |
| M5-F02 | Component identity could be duplicated into a standalone table | P1 | Closed for V1: typed owner payload only; no `core_geometry_component` |
| M5-F03 | Geometry split/merge could be enabled by UI before lineage semantics exist | P0 | Closed: defer formal descriptors; Candidate preview only |
| M5-F04 | Geometric coincidence could create false semantic relationships | P0 | Closed: predicate, topology constraint and typed relationship are separate |
| M5-F05 | Shared Object runtime edits could leak across Maps | P0 | Closed: Map/Branch Command is History-local; shared base-definition edit is separate |
| M5-F06 | Source field revisions could be confused with historical terrain events | P0 | Closed: Map-wide retroactive Source Field Baseline per ME-05-01 |
| M5-F07 | Coordinate spaces and tolerances could be mixed | P1 | Closed at contract level; typed API and numeric fixtures remain open |
| M5-F08 | Projection/renderer caches could overwrite authored curves or formal geometry | P0 | Closed: derived-only; explicit formal conversion required |
| M5-F09 | Asset bytes could be treated as published before metadata commit | P0 | Closed at protocol level; provider/runtime proof remains open |
| M5-F10 | Field, geometry and derived artifacts could be conflated in one Asset namespace | P1 | Closed: Core Asset, Derived Artifact and presentation cache remain separate |
| M5-F11 | Numerical uncertainty could be forced into a Boolean answer | P1 | Closed at result contract level: Indeterminate/Unsupported are explicit |
| M5-F12 | Cross-platform precision and measurement claims could be made without evidence | P1 | Open implementation gate: Windows/macOS fixtures and selected-library proof required |

## 10. V1 implementation gates and acceptance matrix

### Gate A — Core Domain authority
- Reconcile actual enabled Command descriptors and payload validators with P2-09-15/17.
- Verify Geometry Component role/cardinality constraints against P2-09-18 and Object Type fixtures.
- Verify Relationship descriptors against P2-09-20; no implicit relation from geometry.
- Verify Shared Object definition, Map Initial State and History write paths are distinct.
- Verify Field Dataset/Revision metadata and CAS against P2-09-11/12/30/31.

### Gate B — Canonical geometry and field packages
- Freeze geometry envelope/grammar only after fixture and canonical-byte tests.
- Verify ME-05-07 field manifest/tile directory, no-data, coverage and edge behavior; implement and validate the ME-05-08 header/directory parser and writer against P2-09-33 fixtures.
- Verify content hash/read-back integrity and typed Asset ownership.
- Verify reference-aware retention and safe orphan reconciliation.

### Gate C — Spatial services
- Validate robust predicate behavior for point/line/polygon/multipart and supported curves.
- Test boundary, holes, degeneracy, wrap/seam and precision-indeterminate cases.
- Verify query generation/fingerprint freshness and cancellation.
- Validate LocalCartesian and Geographic transform/measurement behavior.
- Establish curve length/area error bounds or return Unsupported.

### Gate D — Specialist semantics
- ME-02 River/Lake and land/water constraints use shared predicates but keep domain-specific semantics.
- ME-03 roads/settlements define junction, routing and adjacency semantics explicitly.
- ME-04 entrances/floors/rooms define access and local-space connectivity explicitly.
- AI Candidates retain provenance, uncertainty and formal target context.

### Gate E — Platform and recovery proof
- Windows and macOS canonical serialization/hash fixtures.
- SQLite provider transaction, composite-FK and immutability behavior.
- Process interruption between Asset write and metadata commit.
- Stale CAS/fingerprint rejection and idempotent commit reconciliation.
- Spatial predicate/measurement precision and performance on representative World/Region/Locality/Site workloads.
- Derived cache loss/rebuild without formal data loss.

No gate is marked passed by this design review. Each requires implementation/test evidence.

## 11. ME-05 completion status

ME-05-01 through ME-05-11 are documented as adopted/recommended design working contracts, including the Field Dataset authority model, shared geometry/spatial services, canonical field package, Parser/Writer contract, golden fixtures and developer handoff. This closes the **ME-05 design package at working-baseline level**, not production implementation.

Outstanding implementation work:
- final generic geometry JSON grammar and canonical serializer (separate from the ME-05-08 Field Package binary format);
- ME-05-10 Parser/Writer implementation and execution of the P2-09-34/35/36 vectors and golden fixtures; selected codec/precision profiles; tile-size and random-read benchmark;
- selected projection/geometry/geodesic algorithms and their licenses/performance;
- exact precision profiles and numeric fixtures;
- specialist domain validation and enabled descriptor reconciliation;
- bundled SQLite provider proof and Asset GC/recovery;
- Windows/macOS runtime execution evidence.

No library, physical schema amendment, API, command descriptor or test pass is implicitly approved by this review.

## References

- ME-05-01 Field Dataset Authority & Persistence Contract V0.5.
- ME-05-02 Shared Geometry Model & Component Operations V0.1.
- ME-05-03 Topology & Spatial Predicate Contract V0.1.
- ME-05-04 Coordinate Reference, Precision & Measurement Contract V0.1.
- ME-05-05 Geometry & Field Asset Package and Publication Contract V0.3.
- ME-05-07 Field Payload Package & Tiling Contract V0.5.
- ME-05-08 Field Package Binary Envelope & Directory Specification V0.3.
- ME-05-09 Canonical Field Package Manifest & Golden Byte Fixtures V0.1.
- ME-05-10 Field Package Parser/Writer Implementation Contract V0.1.
- ME-05-11 Field Package Parser/Writer Developer Handoff & Test Strategy V0.1.
- P2-09-33 Field Package Binary Envelope Validation V0.1 (28 design vectors; not executed).
- P2-09-34 Field Package Canonical Manifest Schema V0.1.
- P2-09-35 Field Package Canonical Manifest Validation Vectors V0.1 (28 design vectors; not executed).
- P2-09-36 Field Package Parser/Writer Conformance Vectors V0.1 (41 design vectors; not executed).
- P2-09-32 Field Package/Tiling Validation V0.1 (45 design vectors; not executed).
- ME-01-07 Cross-architecture Consistency Review V0.1.
- P2-09-11 Core Domain Physical Schema V0.11.
- P2-09-12 SQLite Provider / Transaction Proof V0.6.
- P2-09-15 Command Descriptor Catalog V0.5.
- P2-09-17 Command Payload Envelope and Validation Contract V0.5.
- P2-09-18 Object Type Descriptor Contract V0.2.
- P2-09-19 Geometry Component Identity and Payload Contract V0.2.
- P2-09-20 Relationship Descriptor and Temporal Contract V0.2.
- P2-09-25 V1 Geometry Asset Encoding and Coordinate Precision Decision V0.1.
- Map Engine Spatial Presentation & Geometry Interaction Contract V0.1.
- ME-02-F Calculation Contracts V0.3.


## 12. V0.5 Field Package implementation-contract reconciliation

ME-05-09, ME-05-10 and ME-05-11 complete the design/developer handoff chain for the field package subsystem. The scope is deliberately precise:

- ME-05-07 defines the logical field package, tiling, coverage, edge, no-data and random-access contract.
- ME-05-08 defines the WVFLDPK binary envelope and dense directory layout.
- ME-05-09 defines canonical manifest bytes and golden package fixtures.
- ME-05-10 defines production .NET Reader/Writer behavior, validation order, bounded resource policy, deterministic output and publication boundary.
- ME-05-11 defines the developer implementation sequence and test strategy.
- P2-09-34/35/36 provide machine-readable schema and design conformance corpora.

The WVFLDPK Field Package is not the generic vector Geometry Asset envelope. Geometry Assets continue to follow ME-05-04/P2-09-25. Field package payloads follow ME-05-07/08 and are owned by the Field Revision publication path in ME-05-01. Do not make the Field Package Parser a generic Geometry Asset parser or duplicate Core Domain Asset publication authority.

### 12.1 Required implementation evidence

The developer team must:
1. Execute all P2-09-34/35/36 manifest, binary-envelope and Parser/Writer vectors against production validators.
2. Verify byte-identical golden outputs on Windows and macOS.
3. Reopen Writer output through the production Reader and verify all tiles and hashes.
4. Demonstrate bounded memory, cancellation and random tile reads under the handoff resource policy.
5. Benchmark tile size and selected codec/precision profiles before freezing production values.
6. Verify the Field Revision publication coordinator owns typed Asset registration, immutable revision creation and current-pointer CAS.
7. Record Asset orphan recovery and failed metadata-commit behavior.

None of these gates is marked passed by this review. The 28/28/41 vectors are not execution evidence.
