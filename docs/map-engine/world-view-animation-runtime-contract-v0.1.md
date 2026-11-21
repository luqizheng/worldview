# World View — Animation Runtime Contract

- Version: 0.1
- Date: 2026-11-21
- Status: Recommended technical baseline
- Scope: Historical-time-driven Unit pose evaluation, movement paths, orientation, trails, playback sampling and rendering integration
- Depends on: World View Architecture Baseline V1; Core Domain Command / State Change / deterministic replay contracts; Map Rendering & Visualization Architecture V0.3; Renderer Contract and Scene Model V0.1; Spatial Presentation Contract V0.1
- Decision owner: World View architecture

---

## 1. Executive decisions

Animation Runtime is a deterministic presentation subsystem. It calculates how Units should appear at a requested Historical Time. It is not a second history engine, physics simulation engine or authoritative owner of Unit movement.

Adopt these V1 decisions:

1. **Historical Time is the only time input that determines a Unit's historical pose.** Animation Runtime must not use elapsed wall-clock time or accumulated frame deltas to decide historical position.
2. **Core Domain owns formal history.** Commands, Events and State Changes define historical facts and reconstruct the authoritative World State.
3. **Animation Runtime owns visual pose evaluation.** Given an immutable World State snapshot, relevant movement definitions and a requested Historical Time, it computes a deterministic pose snapshot.
4. **Timeline owns the selected Historical Time and playback mapping.** It converts playback controls and elapsed real time into a requested Historical Time; it does not calculate Unit positions.
5. **Renderer only draws the supplied pose snapshot.** It does not advance movement, infer history or mutate Unit data.
6. **A movement Command is authoring authority; its generated State Changes remain Core Domain outputs.** A continuous Command may produce multiple meaningful State Changes. Visual interpolation samples are not State Changes.
7. **Animation is evaluated as a pure time query.** Seeking, reverse playback, pause/resume and different frame rates must yield the same pose for the same Map/Branch and Historical Time.
8. **Movement path, timing and speed profile are explicit.** Do not infer a Unit's route or speed from screen-space movement.
9. **World/map coordinate semantics determine movement calculation.** Local fictional maps use their declared local metric; Earth-referenced maps must explicitly choose planar or geodesic distance semantics where the distinction matters.
10. **V1 supports deterministic path-based Unit movement, orientation, trails and multiple Units.** Full physics, collision simulation, autonomous pathfinding and network synchronization are out of scope.

The system should be simple enough for a single-user desktop application while preserving historical correctness.

## 2. Responsibility boundaries

```text
Timeline
  ├─ selected Historical Time
  ├─ play / pause / reverse / seek / speed
  └─ maps wall-clock playback to Historical Time
           │ requested Historical Time
           ▼
Core Domain Historical State Engine
  ├─ Map / Branch history resolution
  ├─ Command / Event / State Change semantics
  └─ authoritative Unit state at requested time
           │ immutable World State snapshot
           ▼
Animation Runtime
  ├─ movement definition resolution
  ├─ deterministic path/time evaluation
  ├─ position and orientation calculation
  ├─ trail calculation
  └─ immutable Unit Pose Snapshot
           │
           ▼
Presentation Scene
  ├─ combines static and animated render items
  └─ applies visibility / layer / style
           │
           ▼
Renderer
  └─ draws the requested frame
```

Narration System is a peer presentation subsystem:

```text
Timeline ─────────────┬───────────────┐
                      ▼               ▼
              Animation Runtime   Narration System
                      │               │
                Unit Pose          Narration state
                      │               │
                      └───────┬───────┘
                              ▼
                       Presentation UI
```

Narration may synchronize with the same Historical Time and Events, but it does not own movement or Unit pose.

### 2.1 Core Domain owns

- Unit identity and formal Unit state;
- movement Commands and their authoring inputs;
- Events and State Changes;
- movement start/end and pause/resume semantics where represented by formal history;
- deterministic Command execution and State Change generation;
- historical state reconstruction for a Map or Branch;
- validation of Unit state transitions and movement constraints.

### 2.2 Timeline owns

- current selected Historical Time;
- playback direction and playback speed;
- play, pause, stop, seek and step controls;
- mapping from real elapsed time to Historical Time;
- playback loop/range policy, if enabled;
- notification that a new time sample is requested.

Timeline does not integrate Unit position.

### 2.3 Animation Runtime owns

- resolving which movement definition applies at a requested time;
- deterministic path evaluation;
- visual position and orientation;
- optional visual trail/route progress;
- animation sampling and pose caching;
- stable Unit pose snapshot generation.

It does not create formal historical facts or edit Core Domain state.

### 2.4 Renderer owns

- drawing Unit symbols, labels, direction indicators and trails;
- visual interpolation between presentation frames only when that interpolation does not change the requested historical-time pose;
- clipping, layering and rendering performance.

Renderer does not calculate authoritative movement.

## 3. The central rule: pose is a function of time

The central contract is:

```text
Pose = Evaluate(
    MapOrBranch,
    HistoricalTime,
    AuthoritativeWorldState,
    ApplicableMovementDefinitions,
    PresentationConfiguration)
```

For the same authoritative inputs and compatible algorithm version, the result must be identical within the documented numeric tolerance.

The following are forbidden as hidden inputs to historical pose:

- previous rendered frame's position;
- elapsed time since the last render;
- current monitor refresh rate;
- current CPU/GPU scheduling;
- wall-clock time;
- unseeded randomness;
- pointer position or UI selection;
- renderer frame count.

### 3.1 Why frame-delta integration is rejected

This pattern is not valid for historical truth:

```text
unit.position += unit.speed * frameDelta
```

It makes the result depend on how many frames were rendered. Dropped frames, seeking, reverse playback and a different display refresh rate can produce different positions.

Instead, calculate the pose from the requested Historical Time and the movement's deterministic time/path definition.

## 4. Time model

### 4.1 Historical Time versus wall-clock time

World View has two distinct clocks:

**Historical Time**
- belongs to the Map's timeline;
- is the authoritative time axis for historical reconstruction;
- may represent fictional calendars or real historical dates;
- is the input to Animation Runtime.

**Wall-clock time**
- is elapsed real time on the user's computer;
- is used by Timeline only to advance playback while playing;
- is not persisted as the historical time of a Unit's movement.

Timeline maps the two:

```text
real elapsed duration × playback rate × direction
                         │
                         ▼
                 Historical Time delta
                         │
                         ▼
                 requested Historical Time
```

A playback rate of 2× means Historical Time advances twice as fast as real time. Reverse playback makes the Historical Time delta negative. Neither changes the underlying movement definition.

### 4.2 Historical Time precision

Animation Runtime consumes the canonical Historical Time representation supplied by Core Domain. It must not invent a second timestamp format.

The current Core Domain time contract preserves time precision and ordering semantics. Animation sampling must respect that contract:
- do not silently convert a coarse historical time into a falsely exact instant;
- if a requested time is an interval/uncertainty range, use an explicit sampling policy;
- if the pose differs materially across that interval, expose the ambiguity rather than choosing an arbitrary point.

For normal playback, Timeline should request a concrete sample position using the Map's canonical time arithmetic. For historical records with coarse precision, the user-facing UI may show an uncertainty band or an explicitly selected representative instant, but this is a presentation decision and must not rewrite the stored historical time.

### 4.3 Boundary semantics

Movement definitions must declare their interval convention. Recommended V1 convention:

- movement begins at its effective start time;
- movement is active on `[start, end)`;
- at the exclusive end boundary, the post-movement authoritative Unit state applies;
- a zero-duration instantaneous transition is represented by Core Domain state semantics, not by a fabricated animation interval.

If a movement is paused, its execution progress stops while World Historical Time continues. This follows the Core Domain sustained-Command semantics.

## 5. Movement as a formal historical operation

### 5.1 Authoring intent

A movement authoring operation should describe the intended historical action, for example:

> Unit A moves from the northern gate to the river crossing over three days, pauses overnight, then continues to the southern camp.

The user-facing Command expresses intent and inputs such as:
- target Unit;
- route/path;
- start time;
- end time or duration;
- optional speed profile;
- optional pause intervals;
- optional movement mode/measurement semantics;
- any domain-significant phase transitions.

The Command descriptor defines validation and deterministic execution semantics.

### 5.2 Command and State Change relationship

The Command is the authoring authority.

A movement Command may:
- generate no State Changes if it is rejected or produces no formal state transition;
- generate one State Change for a simple movement outcome;
- generate multiple State Changes for meaningful phases, such as departure, arrival, capture of a location, change of Unit status or other domain-significant transitions.

Intermediate visual samples along the path are not separately authored State Changes.

If a continuous Command generates time-phased State Changes, Core Domain owns their generation and replay. Animation Runtime consumes the reconstructed result and movement definition; it must not independently generate competing formal history.

### 5.3 Movement definition versus current Unit state

The system must be able to answer both:

- **What is the Unit's formal state at this Historical Time?** Core Domain.
- **Where and how should the Unit be drawn at this Historical Time?** Animation Runtime.

For example, a Unit may formally change status to `arrived` at the end time. During the interval before arrival, Animation Runtime calculates its in-transit pose. At and after arrival, the authoritative post-arrival state and its position take precedence.

## 6. Movement path model

### 6.1 Path is not a screen-space curve

A movement path is expressed in the Map's authoritative domain coordinate system or in a declared geographic route representation. It must never be stored in viewport pixels.

The path may be:
- a sequence of line segments;
- a sequence of Bézier segments;
- a mixed line/Bézier path;
- a route referencing existing map geometry;
- a route composed of explicit waypoints.

A path reference must preserve its source semantics and revision behavior. If the path references editable geometry, the movement Command must capture enough immutable path input or an immutable asset/reference to preserve deterministic replay. Historical movement must not silently change because a user later edits a road or river used as its route.

### 6.2 Path identity and immutability

Recommended V1 rule:

- a movement Command stores or references an immutable movement path definition;
- editing the source map geometry does not retroactively rewrite the route of an existing historical movement;
- if the author wants the movement to follow a changed route, they explicitly edit/re-author the movement Command under Core Domain rules.

This avoids a historical paradox where yesterday's Unit route changes because today's road geometry was edited.

### 6.3 Path parameterization

A geometric path alone does not define movement timing. Animation Runtime needs a deterministic mapping:

```text
Historical Time → normalized path progress u ∈ [0, 1]
```

Recommended V1 options:

**Constant speed**
- progress is based on cumulative path distance;
- the Unit advances equal distance per equal active-time interval.

**Piecewise speed profile**
- the route is divided into ordered time/distance phases;
- each phase defines a deterministic speed or duration;
- useful for acceleration, pauses and historically meaningful stages.

**Explicit time-position keyframes**
- author supplies time-position samples;
- interpolation between samples is deterministic and declared;
- use only when the historical source actually provides those positions or the author needs direct control.

V1 should implement constant speed and piecewise speed profiles. Explicit time-position keyframes can be added where a real authoring need justifies them.

### 6.4 Distance parameterization

For a polyline or Bézier path, build a deterministic cumulative-length lookup table.

For Bézier curves:
- retain the original curve;
- adaptively subdivide to a declared geometric error tolerance;
- compute cumulative arc-length estimates;
- map requested distance to a segment and local curve parameter;
- refine the local parameter with a bounded numerical method if required.

The lookup table is derived cache data. It must be reproducible from the immutable path definition and recorded algorithm version/tolerance.

Do not assume the Bézier parameter `t` is proportional to distance. Uniform increments in `t` generally do not produce constant speed.

## 7. Distance and coordinate semantics

### 7.1 Local Cartesian maps

For a local Cartesian Map:
- use the Map's declared coordinate unit and metric;
- if its units represent physical distance, calculate path length in those units;
- if it is an abstract drawing space, movement speed is expressed in drawing units per Historical Time or is defined by normalized duration along the route;
- never display a physical speed or scale without a valid unit/scale definition.

### 7.2 Earth-referenced maps

For Earth-referenced maps, the movement definition must declare how route distance is interpreted.

Recommended V1 policy:
- use a suitable projected planar metric for local/regional movement where projection distortion is acceptable and documented;
- use geodesic distance/interpolation for long-distance or global routes where Earth curvature matters;
- do not use raw longitude/latitude Euclidean distance as if degrees were uniform metres.

The choice is part of the movement definition/coordinate context, not a renderer preference.

### 7.3 Projection independence

Changing the display projection or viewport camera must not change the Unit's historical route progress.

The movement calculation occurs in the route's declared domain/metric semantics. The resulting domain position is then transformed by Spatial Presentation for display.

## 8. Position evaluation algorithm

For a movement with start `T0`, end `T1`, and no pauses:

```text
if T < T0:
    use pre-movement authoritative pose
else if T >= T1:
    use post-movement authoritative pose
else:
    progress = elapsed(T0, T) / duration(T0, T1)
    distance = speedProfile(progress or elapsed)
    pathPosition = evaluatePathAtDistance(distance)
    pose = position + orientation derived from path
```

The actual implementation must use Core Domain's canonical Historical Time arithmetic rather than floating-point timestamps.

### 8.1 With pauses

For a sustained Command with pause/resume schedule:

1. Resolve the movement's active execution intervals from the authoritative schedule.
2. Calculate total active execution time elapsed at the requested World Historical Time.
3. Convert active execution time to route progress using the speed profile.
4. Evaluate the path at the resulting distance.
5. During a pause, the Unit remains at the pause position while World Historical Time continues.
6. After resume, movement continues from the previous active progress.

The pause schedule must be derived from formal, deterministic Command inputs and semantics. Do not implement pauses as UI-only timer state.

### 8.2 At boundaries and interruptions

If a Unit receives a new formal command while moving:
- Core Domain determines whether the previous movement is cancelled, superseded, paused or otherwise transitioned;
- the new authoritative history determines the applicable movement definition;
- Animation Runtime does not guess which command wins.

At the exact effective time of a State Change, follow Core Domain's ordering and boundary rules. If two applicable operations are ambiguous, surface the reconstruction/ordering issue rather than blending two incompatible movement paths.

## 9. Orientation and heading

### 9.1 Default orientation

For a Unit following a path, derive its orientation from the local path tangent at the evaluated position.

- line segment: use segment direction;
- Bézier segment: use the derivative at the evaluated curve parameter;
- at a corner: use a deterministic corner policy;
- at a pause: retain the last valid movement heading unless a formal orientation State Change says otherwise;
- at zero-speed/degenerate tangent: use the last valid heading or the Unit's authoritative orientation.

### 9.2 Corner policy

Recommended V1:
- use the outgoing segment tangent at a segment boundary for forward motion;
- use the incoming tangent when approaching from the reverse direction;
- optionally apply a visual-only turn interpolation over a small declared presentation interval, provided the position remains the exact historical-time path position and the heading result is deterministic.

Do not smooth a corner by moving the Unit away from the formal path.

### 9.3 Coordinate-frame heading

For geographic routes, calculate the local tangent in the correct geographic/projected frame and convert it to the presentation orientation convention. Do not treat longitude/latitude axis differences as a screen rotation.

Renderer receives a normalized presentation orientation, such as radians in the map/presentation frame. The exact symbol rotation convention belongs to the renderer/style contract.

## 10. Trails and route visualization

Separate three concepts:

1. **Planned route** — the full path associated with a movement definition.
2. **Traversed trail** — the portion of the path already traversed at the requested Historical Time.
3. **Historical track evidence** — authored/imported observations of where the Unit was recorded to be.

These are not interchangeable.

V1:
- support planned route display;
- support traversed trail derived from the movement path and current progress;
- preserve a distinct data type for observed historical tracks if introduced;
- do not label a simulated/interpolated route as an observed track.

Trail visibility, color, width and fade are presentation settings. Trail geometry is derived from the immutable movement definition and requested time; it is not persisted as a new set of State Changes at every frame.

For a paused Unit, the traversed trail ends at the pause location. After resume, it extends deterministically.

## 11. Multiple Units

Animation Runtime must evaluate multiple Units at the same requested Historical Time.

Recommended V1 approach:
- batch evaluation by Map/Branch and Historical Time;
- reuse the single reconstructed World State snapshot;
- group movement definitions by source path/metric/algorithm version where useful;
- compute immutable pose records in a deterministic order;
- isolate failures per Unit where safe.

One malformed movement definition must not prevent unrelated Units from rendering. The failed Unit should produce a typed diagnostic and use a safe fallback policy, such as its authoritative static position if valid.

V1 does not perform collision avoidance or automatically resolve Units occupying the same location. Those are domain/simulation capabilities and are deferred.

## 12. Determinism, precision and algorithm compatibility

### 12.1 Determinism requirements

For the same:
- Map or Branch identity;
- requested Historical Time;
- authoritative World State;
- immutable movement path and speed profile;
- movement Command descriptor/handler semantics;
- distance model and projection-independent metric settings;
- algorithm version;

the Animation Runtime must return the same pose within a defined numerical tolerance.

Do not use:
- unseeded randomness;
- machine locale;
- current culture-specific number parsing;
- current wall-clock time;
- non-versioned external routing APIs;
- nondeterministic collection iteration as a tie-breaker.

### 12.2 Numeric policy

- Use double precision for spatial calculations in V1.
- Use canonical Historical Time arithmetic from Core Domain for time comparisons and elapsed-time calculations.
- Define tolerances separately for distance, position and orientation.
- Do not round positions to display pixels before pose snapshot creation.
- Renderer may apply pixel alignment as a visual operation only.

### 12.3 Algorithm versioning

Movement Command replay and visual pose evaluation have related but distinct compatibility concerns.

Core Domain must preserve the recorded Command descriptor/execution semantics required to reproduce formal State Changes.

Animation Runtime must also identify the pose-evaluation algorithm version used by a cached or reproducibility-sensitive result. A visual algorithm improvement must not silently change formal history. If a historical presentation must be exactly reproducible for archival/export, its presentation algorithm/configuration must be pinned or recorded as export metadata.

For ordinary interactive viewing, the current compatible pose algorithm may be used, provided it does not change authoritative World State or claim that a changed visual interpolation is a changed historical fact.

## 13. Pose snapshot contract

Conceptual data shape:

```csharp
public sealed record UnitPoseSnapshot(
    ObjectId UnitId,
    HistoricalTime EvaluatedAt,
    DomainPoint Position,
    PresentationHeading Heading,
    UnitMotionState MotionState,
    PathProgress? PathProgress,
    ImmutableArray<TrailSegment> VisibleTrail,
    PoseDiagnostics Diagnostics);

public enum UnitMotionState
{
    Stationary,
    Moving,
    Paused,
    Arrived,
    Unknown
}
```

This is a conceptual API shape, not a frozen database schema.

Requirements:
- immutable after publication;
- includes the evaluated Historical Time;
- includes source Unit identity;
- contains presentation-ready position/heading;
- may include progress and trail data;
- carries diagnostics when evaluation is approximate or degraded;
- does not become formal Core Domain state;
- does not contain mutable references to UI controls or renderer objects.

A Pose Snapshot is a derived read model and can be discarded/recomputed.

## 14. Cache and invalidation

Recommended initial caches:

| Cache | Key | Invalidation |
|---|---|---|
| Movement path metric table | Immutable path identity/content hash + metric semantics + algorithm version | Path/metric/algorithm change |
| Movement schedule | Command identity + descriptor semantics + schedule inputs | Command edit/re-authoring or semantics change |
| Pose snapshot | Map/Branch + Historical Time + World State generation + movement set generation + algorithm version | Any dependency changes |
| Trail geometry | Movement identity + progress + path/algorithm version | Time/progress/path change |

Start with path metric tables and per-time pose snapshots. Avoid building a complex animation cache framework before profiling.

A cache must never be used if its dependency identity is incomplete. A stale pose snapshot must not be presented as current after the World State or movement definition changes.

## 15. Playback and frame scheduling

### 15.1 Timeline playback

Timeline advances Historical Time using elapsed real time and the selected playback rate.

Example:
- 1×: one historical day per real day only if the configured playback scale says so;
- 60×: sixty times that historical-time rate;
- reverse: negative Historical Time progression.

The exact scale unit is configurable. Timeline must make it explicit; Animation Runtime does not assume that one real second equals one historical second.

### 15.2 Frame sampling

For each requested presentation frame:
1. Timeline publishes the requested Historical Time.
2. Core Domain provides/reuses the authoritative World State at that time.
3. Animation Runtime evaluates Unit poses.
4. Presentation Scene receives the pose snapshot.
5. Renderer draws the frame.

The UI may coalesce rapid time requests and skip intermediate display frames to remain responsive. It must still calculate the correct pose for the latest requested Historical Time.

### 15.3 Playback smoothness versus historical correctness

If the display runs at 60 FPS, it samples the historical pose at each corresponding Timeline time. If it runs at 30 FPS, it samples fewer times. The pose at any shared Historical Time must be the same.

Renderer may interpolate only between two samples when that interpolation is mathematically equivalent to evaluating the deterministic pose function at the intermediate Historical Time. Prefer requesting/evaluating the actual target time when practical.

Never use render-frame interpolation to conceal a discontinuity in formal history or to cross an unresolved Command boundary.

## 16. Timeline seek, reverse, pause and resume

Required behavior:

**Seek**
- Timeline selects an arbitrary Historical Time;
- Core Domain reconstructs the state at that time;
- Animation Runtime evaluates directly at that time;
- no replay of every rendered frame is required.

**Reverse**
- Timeline decreases Historical Time;
- Animation Runtime evaluates the same pose function at the earlier time;
- do not reverse an accumulated velocity simulation.

**Pause**
- Timeline stops advancing Historical Time;
- the same requested time yields the same pose;
- a historically paused sustained movement remains paused according to its formal schedule, independently of whether the user paused playback.

**Resume**
- Timeline resumes advancing from its current Historical Time;
- no positional jump is introduced by wall-clock accumulation.

**Different frame rates**
- 24, 30, 60 or 120 FPS must not alter the pose at the same Historical Time.

## 17. Narration integration

Narration System and Animation Runtime are synchronized peers.

- Both consume the same selected Historical Time.
- Narration may resolve active narration segments and Event associations.
- Animation Runtime resolves Unit poses.
- An Event may trigger both a narration segment and a Unit state/movement change through the formal history model.
- Narration playback position may have its own text/audio timing, but its historical association remains anchored to the Map's Historical Time.
- Hiding narration must not stop Unit movement.
- Hiding animation must not stop narration.
- A narration passage may describe a movement that is not visually represented; absence of animation does not invalidate the narrative.
- Animation must not generate narration or infer historical facts from visual motion.

## 18. Export and reproducibility

For export at a single Historical Time:
- reconstruct the requested World State;
- evaluate poses at that exact time;
- render a static frame.

For video export:
- define an explicit mapping from output frame index to Historical Time;
- evaluate each frame independently or through a deterministic batch evaluator;
- do not depend on real-time playback scheduling;
- pin the required rendering/presentation configuration if reproducible archival output is required;
- record Map/Branch, time range, playback scale, frame rate and relevant algorithm/configuration versions as export metadata.

Export should produce the same frame at a given Historical Time regardless of whether it was generated sequentially or by seeking directly to that time.

## 19. Failure and fallback behavior

Handle:
- missing or invalid movement path;
- invalid speed profile;
- zero-length path with nonzero duration;
- non-positive duration where the descriptor requires positive duration;
- invalid geographic/projection region;
- degenerate curve/tangent;
- ambiguous overlapping movement Commands;
- stale World State or path cache;
- unsupported historical algorithm version;
- missing referenced immutable path asset.

Rules:
- return typed diagnostics;
- do not fabricate a route or position;
- do not silently fall back to a different path;
- where the authoritative Unit position is valid, display a stationary/fallback symbol with an indication that motion could not be evaluated;
- if formal history reconstruction itself fails, do not pretend that a valid pose exists;
- isolate per-Unit presentation failures when safe.

## 20. V1 scope and explicit exclusions

### Included in V1

- multiple Units;
- path-based movement;
- line and Bézier movement paths;
- constant-speed movement;
- piecewise speed profiles;
- formal pause/resume schedule support where defined by Core Domain;
- deterministic position and heading;
- planned route and traversed trail;
- Timeline play/pause/reverse/seek integration;
- static-frame and deterministic video-export evaluation;
- per-Unit diagnostics.

### Deferred

- rigid-body physics;
- collision detection/avoidance between Units;
- autonomous pathfinding;
- terrain-aware movement simulation;
- formation AI and tactical behavior;
- stochastic battle simulation;
- networked synchronized animation;
- skeletal/character animation;
- 3D animation;
- automatic inference of missing historical movement;
- complex acceleration/vehicle dynamics unless a concrete use case requires them.

These may become separate simulation or presentation capabilities. They must not be smuggled into the V1 historical pose evaluator.

## 21. Verification plan

### AR-01 — Deterministic time evaluation
- evaluate same time repeatedly;
- evaluate time sequence forward and reverse;
- seek directly to a time versus sequential playback;
- compare results at common times across 24/30/60/120 FPS sampling.

### AR-02 — Path evaluation
- line path;
- quadratic and cubic Bézier path;
- mixed path;
- constant-speed arc-length evaluation;
- piecewise speed profile;
- path with zero-length/degenerate segment.

### AR-03 — Time and pause schedule
- movement start and exclusive end boundary;
- pause/resume with World Historical Time continuing;
- multiple pause intervals;
- reverse playback across pause boundaries;
- formal interruption/supersession by a later Command.

### AR-04 — Orientation
- straight line;
- curve tangent;
- corner incoming/outgoing policy;
- pause heading retention;
- zero-speed and degenerate tangent fallback.

### AR-05 — Coordinate semantics
- local Cartesian map units;
- physical-unit local map;
- regional projected Earth map;
- geodesic long route;
- change display projection without changing route progress.

### AR-06 — Multiple Units and resilience
- many simultaneous Units;
- independent paths and speed profiles;
- one invalid Unit path does not break others;
- deterministic ordering and stable IDs.

### AR-07 — Core Domain boundary
- visual interpolation does not create State Changes;
- meaningful continuous Command phases are generated only by Core Domain;
- movement path is immutable for replay;
- later source geometry edits do not retroactively alter the route;
- formal state at arrival overrides in-transit pose at the boundary.

### AR-08 — Narration and export
- narration and animation synchronize to the same Historical Time;
- hide/show narration independently;
- direct-seek export frame equals sequentially generated frame;
- video export is independent of real-time playback frame drops.

## 22. Acceptance criteria

The Animation Runtime contract is ready for implementation when:

1. Timeline is the sole owner of selected playback Historical Time.
2. Core Domain is the sole authority for formal Unit history and state.
3. Animation Runtime is a pure deterministic evaluator of requested-time poses.
4. No historical position depends on previous rendered frames or wall-clock delta.
5. Movement paths and speed schedules are immutable/reproducible inputs.
6. Path distance semantics are explicit for local and Earth-referenced maps.
7. Bézier paths use arc-length-aware evaluation rather than assuming parameter `t` is distance.
8. Position, heading and trail are computed consistently at arbitrary Historical Time.
9. Pauses stop movement execution progress while World Historical Time continues, according to the formal sustained-Command schedule.
10. Visual interpolation samples never become State Changes.
11. Formal time-phased State Changes are generated only by Core Domain.
12. Multiple Units can be evaluated in one batch without shared mutable animation state.
13. Seek, reverse, pause/resume and different frame rates preserve same-time pose consistency.
14. Narration and animation synchronize through Historical Time but remain independently controllable.
15. Static and video exports are deterministic with respect to their declared inputs.
16. Failures are explicit and never fabricate historical movement.

## 23. Final recommendation

Adopt Animation Runtime V0.1 as the V1 technical baseline.

The architecture has four clear authorities:

- **Timeline** decides which Historical Time is being viewed.
- **Core Domain** decides what historically happened and the authoritative Unit state.
- **Animation Runtime** calculates where a Unit should be drawn at that time.
- **Renderer** draws the calculated result.

This lets World View present movement smoothly without turning animation frames into a second source of historical truth. It also preserves the same result when users seek, reverse, pause, export or reopen a Map years later.