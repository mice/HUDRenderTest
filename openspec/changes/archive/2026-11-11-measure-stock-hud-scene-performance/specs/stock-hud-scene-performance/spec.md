## ADDED Requirements

### Requirement: Actual dependency comparison
The comparison SHALL use real custom and stock UGUI compilations with individually frozen source inventories, APK SHA, build GUID, source fingerprint and resolved VertexHelper identity. Ordinary executable inputs SHALL match; dependency and Editor provenance differences SHALL be explicitly reviewed. Stock functional validation SHALL contain actual nonempty successful results.

#### Scenario: Stale stock workspace or mismatched inputs
- **WHEN** stock sources, assets, settings or non-UGUI dependencies differ unexpectedly
- **THEN** the workspace SHALL be refreshed and audited before measurements are accepted

#### Scenario: Custom APK reuse
- **WHEN** a previous frozen custom APK is reused
- **THEN** its original inventory SHALL be preserved and all current input differences SHALL be classified before accepting its applicability

### Requirement: Repeated representative timing
The measurement SHALL register A/B/B/A/B/A/A/B, use all probes off and the fixed representative 256-holder Rotating workload, and accept only eight complete 9006-frame runs. Every sample SHALL validate actual 2340x1080 Screen and camera dimensions. Per-run statistics and actual two-frame Burst-window sums SHALL be reported with the quantile definition.

#### Scenario: Complete balanced comparison
- **WHEN** all eight runs have matching configuration, request/completion topology, resources and final geometry and valid variant-specific identity
- **THEN** timing conclusions SHALL compare the four independent runs per dependency path and disclose environmental limitations

#### Scenario: Invalid run
- **WHEN** identity, geometry, dimensions, completion, thermal status or report integrity fails
- **THEN** the affected group SHALL be rejected or separately retained and SHALL NOT be silently mixed with accepted runs

### Requirement: Bounded Profiler allocation evidence
GC acceptance SHALL use separate Unity Profiler capture with explicit thread, frame, scope, positive control and source provenance. Temporary tooling SHALL remain outside Player build inputs and SHALL NOT add production allocation counters. Conclusions SHALL identify all unverified thread/platform/scope boundaries.

#### Scenario: Main Thread Editor capture
- **WHEN** a live capture provides matching sampled/drain HUD scopes and allocating positive controls
- **THEN** scoped event/byte observations SHALL be reported as Editor Mono Main Thread evidence with raw/live artifacts and no inferred device IL2CPP or full-application result

### Requirement: Restoration and bounded completion
Device execution SHALL preserve and restore the starting APK, request, settings and file inventory and leave the application stopped. This subtask SHALL NOT close PERF-04/M5 or mark an RC, coverage snapshot, tag or release passed without its own precise evidence.

#### Scenario: Completed or interrupted device execution
- **WHEN** device mutation ends normally or fails
- **THEN** restoration SHALL execute and its readback SHALL be recorded alongside the retained measurement evidence
