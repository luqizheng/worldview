# World View — ME-02 UX Scenarios & Acceptance

- Version: 0.1
- Date: 2026-11-21
- Status: **Specialist UX Scenario and Acceptance Working Baseline** — end-to-end natural-geography authoring and review workflows; exact Avalonia layouts, visual tokens and interaction implementation remain design/engineering gates
- Parent: ME-02 Terrain & Natural Geography Editor V0.3
- Depends on: ME-01-03/04/05/06/07; ME-02-03 through ME-02-07; Core Domain Object/Geometry/Relationship/History contracts; Map Engine spatial interaction and renderer-neutral scene contracts

## 1. Purpose and boundary

This document translates ME-02 natural-geography domain workflows into user-visible end-to-end scenarios and acceptance criteria.

It specializes the shared interaction framework; it does not create a second interaction architecture. The Canvas remains the primary spatial surface, the Inspector explains the selected formal Object or derived result, and the single Assistant helps translate intent, explain causes and stage reviewable proposals.

The UX must keep four content classes visually and semantically distinct:

1. **Formal world facts** — confirmed Objects, Geometry Components, relationships and Map/Branch history.
2. **Source fields** — authored/imported physical inputs such as land/water masks and elevation.
3. **Derived analysis** — terrain metrics, drainage, climate, moisture, classification and ecological suitability, each with provenance and freshness.
4. **Candidates and diagnostics** — proposed Object/geometry/relationship changes, uncertain detections and validation findings.

A color overlay alone is insufficient to communicate authority. Use labels, line patterns, icons, layer grouping and Inspector status as redundant cues.

## 2. Shared ME-02 workspace behavior

### 2.1 Primary work surface

- Canvas is the dominant central work surface in every ME-02 activity.
- The tool palette exposes the current specialist operation without replacing shared selection, navigation or edit-session behavior.
- The Inspector is contextual: selected Object, physical field, derived analysis, Candidate or diagnostic.
- Assistant conversation is available from the shared editor shell and can refer to the active Map, Branch, Historical Time, Working Scope and explicitly selected context.
- Calculation progress and diagnostics are visible without blocking unrelated Canvas inspection.
- The user can continue manual editing and inspection when no AI provider is configured or available.

### 2.2 Explicit active context

Before a formal edit or calculation, show the relevant context in a compact, readable way:
- active Map and Branch;
- Historical Time where the operation is history-dependent;
- Working Scope and any expanded calculation context;
- selected Object/field/derived artifact;
- source freshness and protected content where relevant.

The viewport is not an implicit operation boundary. A basin, wind/moisture path or connected terrain feature may extend beyond the visible Canvas. The UI must show the actual affected/calculation scope.

### 2.3 Layer and status vocabulary

Use consistent labels:
- **Authored / Imported** — source input and its provenance.
- **Formal** — Core Domain Object, relationship or committed geometry.
- **Derived · Current** — calculation result matching current inputs.
- **Derived · Stale** — retained output whose dependencies changed.
- **Incomplete** — result with missing coverage or unresolved required inputs.
- **Candidate** — proposed change awaiting review/adoption.
- **Diagnostic** — validation or uncertainty evidence, not a world fact.

Do not label a derived suitability layer as an actual biome, a channel candidate as a River, or a visible line crossing as a confirmed confluence.

## 3. End-to-end scenario A — Establish land/water and elevation

### User intent

“I want to establish the continent outlines and broad elevation before drawing mountains and rivers.”

### Workflow

1. User opens the Land/Water & Elevation specialist mode. The Canvas remains visible; the Inspector shows Map frame, edge semantics and current source-field coverage.
2. User chooses to sketch, import or generate a land/water mask. The editor distinguishes a transient sketch/import preview from formal Landmass Objects and published source fields.
3. The system validates Map extent, coordinate reference, coverage, no-data and edge/wrap semantics. It does not interpret missing coverage as ocean or zero elevation.
4. If semantic Landmass Objects are required, the creator reviews proposed identities and boundaries separately from the physical land/water mask.
5. User authors or imports elevation. The Inspector shows units, vertical reference, resolution, coverage, source and publication state.
6. Terrain brush/feature-guided edits update a transient preview. The user can compare before/after and cancel without changing the published source field.
7. On completion, the field is validated and published as a new immutable source Asset/revision under the field contract.
8. The editor marks only dependent analysis outputs stale and explains the affected scope.

### Acceptance criteria

- [ ] Land/water mask and Landmass semantic Objects are distinct concepts.
- [ ] The UI never silently converts no-data to ocean, land or zero elevation.
- [ ] Map-edge semantics are explicit before calculations that depend on map boundaries.
- [ ] Elevation unit and vertical reference are visible and validated.
- [ ] A preview can be cancelled without changing the authoritative source Asset.
- [ ] Published source revisions are immutable and retain provenance.
- [ ] Downstream invalidation is dependency-based, not a blanket “entire map changed” status.
- [ ] Formal Object creation and source-field publication are separately reported.

## 4. End-to-end scenario B — Author and refine terrain features

### User intent

“Create this mountain range, identify its major peaks, and show the highland region.”

### Workflow

1. User selects the Mountain Range tool and sketches an approximate extent or ridge. The preview is visibly transient.
2. The Inspector explains that a MountainRange is one semantic Object and may have extent/ridge geometry roles; it is not a collection of raster cells.
3. User confirms the Object identity/required definition attributes. Core Domain reservation and creation rules apply; geometry validation is performed against the descriptor.
4. User selects a peak candidate. The system shows source evidence, elevation value, unit, vertical reference and confidence. A candidate is not yet a formal MountainPeak.
5. User accepts a peak as a formal Object. Its point geometry and required elevation semantics are validated.
6. If the peak is semantically part of the range, the user explicitly accepts the MountainPeak → MountainRange structural relationship. Point-in-polygon alone does not create part_of.
7. User creates or refines a TerrainRegion. Its classification is bounded by the registered descriptor; overlapping regions and unresolved boundaries are shown as diagnostics.
8. A later geometry refinement preserves Object identity and creates a new formal geometry state only through the supported Geometry Operation.

### Acceptance criteria

- [ ] MountainRange extent/ridge and MountainPeak point geometry use declared roles.
- [ ] Detection results remain Candidates until explicitly adopted.
- [ ] MountainPeak identity is distinct from the peak marker/geometry.
- [ ] Elevation always includes unit and vertical reference.
- [ ] part_of is a semantic action distinct from spatial containment.
- [ ] Geometry refinement does not create, split or merge semantic Objects.
- [ ] TerrainRegion classification is not used as a generic bucket for climate or arbitrary raster classes.
- [ ] The Inspector exposes provenance and descriptor validation, not only editable display fields.

## 5. End-to-end scenario C — Analyze drainage and adopt a river network

### User intent

“Analyze where water drains, identify the main rivers and connect the tributaries to the receiving lakes.”

### Workflow

1. User requests drainage analysis for a named Working Scope. The UI shows the calculation scope expanded to include the required terrain and basin context.
2. Input validation checks elevation fingerprint, vertical reference, land/water mask, map-edge semantics, no-data and depression policy.
3. The service identifies depressions and presents their classification: confirmed lake basin, confirmed closed basin, candidate sink, data artefact candidate or unresolved.
4. User chooses/accepts a conditioning policy where required. The authored elevation source is never overwritten by the derived conditioned surface.
5. The calculation produces flow direction, accumulation, basin/divide and channel candidates with algorithm/version, parameters, source fingerprints and uncertainty.
6. User reviews candidate River groupings. The UI distinguishes analyzed flow from formal River geometry and confirmed relationship edges.
7. User adopts selected River Objects. Identity allocation and geometry publication are separate from derived channel detection.
8. User reviews each proposed flow direction. The Inspector shows upstream → downstream meaning, existing incoming/outgoing relationships and cycle/cardinality validation.
9. Accepted relationships are applied through the correct Initial State or selected History path. The editor reports the exact formal result only after Core Domain confirms it.

### Acceptance criteria

- [ ] Drainage scope is not silently clipped to the viewport.
- [ ] No-data is never treated as sea level, a sink or an outlet without explicit evidence.
- [ ] Depression treatment is inspectable and does not overwrite source elevation.
- [ ] Flow direction is explicit and never inferred from River centerline vertex order.
- [ ] Derived basins/channels are not formal Objects by default.
- [ ] A geometric crossing is not automatically a confluence.
- [ ] V1 formal flow allows River → River and River → Lake only.
- [ ] A River has at most one downstream receiver; multiple tributaries may enter one River.
- [ ] Lake is terminal in the formal V1 flow graph.
- [ ] Cycles, inactive endpoints and stale source context are surfaced before application.
- [ ] Partial adoption is allowed only when dependency and graph validation prove the subset coherent.

## 6. End-to-end scenario D — Climate-first analysis

### User intent

“Explain why the western coast is wet and the inland plateau is dry, and show the seasonal differences.”

### Workflow

1. User selects a world or region and requests climate analysis.
2. The Inspector lists required planetary/season assumptions and available source inputs: latitude/world frame, elevation, land/water, circulation, moisture sources and ocean influence.
3. Missing material inputs are identified before calculation. The Assistant may ask a focused clarification or apply a named visible default profile when the domain contract permits.
4. The user chooses a seasonal profile. The UI explains that phase count and hemisphere offsets are world parameters, not assumptions derived from a Gregorian calendar.
5. Calculation produces seasonal temperature and precipitation estimates, moisture balance and causal diagnostics.
6. User inspects the causal chain for a region: moisture source → prevailing wind → terrain barrier/uplift → precipitation or rain-shadow effect.
7. Climate classification is shown only after the physical estimates and their coverage/uncertainty are available.
8. The user can compare seasons, inspect assumptions and change inputs. Changed inputs mark dependent outputs stale; a new calculation creates new immutable artifacts.

### Acceptance criteria

- [ ] Climate is not presented as a manually painted authoritative world fact by default.
- [ ] Seasonal phases and planetary assumptions are visible and versioned.
- [ ] The model does not claim exact weather, wind speed or rainfall where V1 only supports broad estimates.
- [ ] A rain-shadow explanation shows the necessary moisture source, wind direction and terrain barrier evidence.
- [ ] Climate classification is downstream of temperature/precipitation estimates.
- [ ] Confidence, coverage and uncertainty are visible at the relevant spatial scope.
- [ ] A stale climate result is not presented as current.
- [ ] Recalculation does not mutate source fields or formal Objects.

## 7. End-to-end scenario E — Ecology-first intent and exception

### User intent

“This island should have a magical forest even though the natural climate model suggests a dry climate.”

### Workflow

1. User describes the intended forest. The Assistant asks whether this is a confirmed world fact, a desired natural outcome or a tentative preference.
2. If confirmed, the system records the creator-authoritative fact through the appropriate World Knowledge/Core Domain path. It does not reject the fact because a suitability model disagrees.
3. The system evaluates climate, water, soil and elevation suitability and shows the causal mismatch.
4. The Inspector displays separate sections for actual creator-confirmed ecosystem and derived biome suitability.
5. The Assistant may suggest plausible explanations (magic, irrigation, unusual species adaptation) as hypotheses, but does not convert them into facts without approval.
6. The user may preserve the exception, alter physical inputs, or revise a soft preference.
7. Species habitat suitability is evaluated separately from actual species presence.

### Acceptance criteria

- [ ] Actual ecosystem, biome suitability and species habitat suitability are separate concepts.
- [ ] A creator-confirmed magical or engineered exception is not treated as invalid merely because natural suitability differs.
- [ ] Derived suitability never automatically creates a TerrainRegion or ecosystem Object.
- [ ] The Assistant labels explanations as hypotheses until confirmed.
- [ ] The UI exposes the cause of divergence without overwriting creator intent.

## 8. End-to-end scenario F — Source change, stale outputs and bounded recomputation

### User intent

“I raised this mountain chain. Update the drainage and climate around it, but keep the confirmed lake and river identities.”

### Workflow

1. User edits the elevation field in a transient session and previews the affected area.
2. On publication, the source Asset revision changes; prior calculations retain their original provenance.
3. The dependency graph identifies affected hydrology and climate outputs. Unrelated ecological or regional outputs remain current only if their dependency manifests prove independence.
4. The UI shows direct source change, affected derived outputs, required context buffer and any global/basin-wide dependency.
5. Existing formal Lake/River Objects and creator-protected geometry are shown as protected facts. The calculation may diagnose conflicts but cannot silently rewrite them.
6. User starts recalculation. Outputs are published atomically; cancellation or failure does not expose partial results as current.
7. Candidate geometry/relationship changes may be proposed separately for review.
8. If a Branch is active, the UI makes clear which History path the formal adoption targets; sibling Maps/Branches remain isolated.

### Acceptance criteria

- [ ] Source Asset revisions are immutable.
- [ ] Freshness is evaluated from declared dependencies, not all Project changes.
- [ ] Non-local dependencies expand calculation scope beyond the edited geometry when required.
- [ ] Protected Objects are never silently overwritten by derived analysis.
- [ ] Derived output publication is atomic and cancellation-safe.
- [ ] Candidate adoption revalidates current History and descriptor versions.
- [ ] A Map/Branch-local operation never changes another Map or sibling Branch.
- [ ] Shared Object definition changes and History-local changes show their different propagation semantics.

## 9. Inspector content contract

The Inspector must be contextual and authority-aware.

| Selected content | Required Inspector sections |
|---|---|
| Landmass | Identity/type, definition attributes, geometry roles, Map/History presence, source/provenance, relationships, validation |
| MountainRange | Identity/type, extent/ridge components, classification, peak membership, contained features, provenance, diagnostics |
| MountainPeak | Identity/type, point geometry, elevation/unit/vertical reference, part-of relationship, source evidence |
| River | Identity/type, centerline components, river class, formal upstream/downstream relationships, analyzed flow evidence, outlet/uncertainty |
| Lake | Identity/type, shoreline, water-body class, incoming formal Rivers, derived basin/water-balance evidence |
| TerrainRegion | Identity/type, terrain classification, extent, overlap/adjacency, source and derivation status |
| Physical field | Field identity/revision, source, units/reference, resolution, coverage/no-data, fingerprint, publication and dependent outputs |
| Derived analysis | Calculation ID, output role, algorithm/version, parameters, input manifest, freshness, coverage, uncertainty, diagnostics |
| Candidate | Candidate identity/version/hash, proposed changes, provenance, dependencies, validation, impact, accept/reject eligibility |
| Diagnostic | Severity, explanation, source evidence, affected references/scope, recommended action |

The Inspector must not display definition fields as if they were History-local runtime fields, or show derived output fields as directly editable formal Object attributes.

## 10. Confirmation and review rules

- Low-risk, unambiguous, small manual edits may apply after validation without a separate confirmation dialog.
- Large-area field publication, broad generation, destructive edits, protected-content impact and formal relationship changes require contextual preview/review according to the relevant Core Domain/AI authorization policy.
- Formal Object identity allocation and creation must be explicit; detection is not creation.
- Relationship direction and cardinality must be understandable in creator language.
- Candidate acceptance is bound to the exact Candidate version/hash and target Map/Branch/Time context.
- A changed Candidate or stale input requires renewed validation and, where material, renewed user decision.
- Confirmation describes concrete impact and whether the action changes a source field, formal world fact, derived artifact or presentation only.

## 11. Error and recovery scenarios

| Situation | Required user-visible behavior |
|---|---|
| Missing elevation or unresolved vertical reference | Explain which analysis is blocked; allow unrelated manual authoring |
| Incomplete/no-data coverage | Show boundary and uncertainty; never fabricate a coastline or outlet |
| Depression classification ambiguous | Preserve as unresolved Candidate/diagnostic; do not auto-fill |
| River candidate has unsupported outlet | Show unresolved terminal evidence; do not invent an Ocean Object or relationship |
| Flow relationship would create cycle or exceed downstream cardinality | Block that relationship and show the conflicting path |
| Climate profile lacks material season assumptions | Ask a focused clarification or use a named disclosed default where allowed |
| Derived output is stale | Keep it inspectable as stale; prevent current-result claims and stale adoption |
| Candidate target changed | Mark stale and require regeneration/review |
| Calculation cancelled or failed | Preserve source/formal state; do not publish partial output as current |
| AI unavailable | Manual authoring, local inspection and deterministic calculations remain available |
| Final Core Domain application fails | Report no formal change for the failed transaction; preserve staged Candidate if safe |

## 12. Accessibility and information density

- Every layer distinction uses more than color alone.
- Diagnostics and Candidate lists are keyboard navigable and link to a reliable Canvas target.
- The Inspector exposes the same essential information without requiring hover.
- Tool activation, active scope and selected formal/Candidate state are announced accessibly.
- Dense scientific detail is progressively disclosed; ordinary creators see causes and ranges first, while exact parameters/provenance remain inspectable.
- Long calculations do not block Canvas navigation or inspection of unaffected content.
- The UI avoids modal wizard chains; questions are contextual and only asked when missing information materially changes correctness.

## 13. Consolidated acceptance checklist

- [ ] Formal Objects, source fields, derived analysis, Candidates and diagnostics have distinct authority and visual semantics.
- [ ] Canvas, Inspector and Assistant have complementary roles and no competing source of truth.
- [ ] Every formal operation resolves an explicit Map/Branch/Time and Working Scope.
- [ ] Viewport bounds never silently truncate a broader requested operation.
- [ ] Preview/cancel does not mutate formal state or published source Assets.
- [ ] Every derived result exposes calculation provenance, freshness, coverage and uncertainty.
- [ ] All ME-02 authoring flows preserve the distinction between detection and formal Object creation.
- [ ] Relationship semantics are explicit and never inferred from geometry alone.
- [ ] Creator-confirmed facts remain authoritative over derived suitability.
- [ ] Calculation failures/cancellation preserve authoritative source and formal state.
- [ ] Candidate acceptance uses the existing AI Assistant authorization and Core Domain validation path.
- [ ] Manual authoring and inspection work without an AI provider.
- [ ] Branch/Map isolation and shared-definition propagation are communicated correctly.
- [ ] Keyboard and accessibility paths cover essential editing, review and diagnostic navigation.

## 14. Status and next gate

ME-02-G is complete as an end-to-end UX scenario and acceptance working baseline. It does not freeze exact Avalonia layouts, docking controls, colors, icons, shortcuts, renderer, geometry engine or raster library.

Next is ME-02-H: cross-architecture review against Core Domain, Map Engine, Map Generation and ME-01, followed by an implementation handoff package that identifies unresolved engineering gates without treating design completion as implementation completion.
