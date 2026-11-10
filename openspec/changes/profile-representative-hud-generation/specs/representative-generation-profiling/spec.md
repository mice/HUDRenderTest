## ADDED Requirements

### Requirement: Opt-in rotating generation phase diagnostics
Representative rotating workloads SHALL expose default-off numeric timing aggregates for completed-holder setup/generation and TMP Canvas update, forced mesh generation, extraction and transform. Disabled, incomplete and stale results MUST be distinguished from measured zero durations, and diagnostic sampling MUST NOT add per-frame collection allocation or log/file output to the hot path.

#### Scenario: Completed bucket is profiled
- **WHEN** a representative Partial or Burst bucket completes with diagnostics enabled
- **THEN** recorded holder and TMP sample counts match the completed work, stages have explicit validity, and inclusive timing relationships are documented without overlapping sums

#### Scenario: Diagnostics are disabled or no work occurs
- **WHEN** a profiled run is followed by disabled diagnostics, an empty refresh or an idle frame
- **THEN** stale stage data are cleared and unavailable fields are not presented as measured zero costs

### Requirement: Diagnostic workload equivalence and overhead
Diagnostics on/off SHALL preserve the same input requests, completion topology, geometry and output. A same-APK repeated target-device comparison MUST report probe disturbance, phase counts, normal-path median/p95 and two-frame Burst cost. Actual Unity rendering dimensions MUST be recorded separately from Android physical display evidence.

#### Scenario: Matching runs are compared
- **WHEN** probe-on and probe-off representative runs use the same APK and workload
- **THEN** requests/completions and final geometry match, rendering dimensions match the registered landscape configuration, and probe overhead is reported before phase attribution

### Requirement: Profiler-based GC acceptance
New GC acceptance SHALL use a separate positive-controlled Unity Profiler capture with bounded sample scopes and raw evidence, without requiring additional production GC counting code. Platform, thread, frame scope, capture validity and available byte semantics MUST be reported. Profiler diagnostic timings MUST NOT be substituted for Release timings.

#### Scenario: GC capture is accepted
- **WHEN** a capture contains a known allocation control and valid warmed HUD samples
- **THEN** allocation evidence is attributed to the documented HUD scope with the control observed, and any zero result is limited to that scope and platform

#### Scenario: Scoped bytes or capture are unsupported
- **WHEN** target profiling cannot provide valid scoped byte evidence or a capture lacks valid frames/control
- **THEN** the corresponding result remains unavailable/unverified, not zero or accepted; no unsafe IL2CPP API or marker duration is used as a byte substitute

### Requirement: Bounded PERF-04 delivery
Conclusions SHALL bind source/build/dependency/request identities, preserve rejected captures and device restoration evidence, and retain original performance budgets and stock/exact-RC/coverage follow-ups.

#### Scenario: Profiling subtask completes
- **WHEN** generation measurement and its validation are complete
- **THEN** documentation records the cost distribution, probe overhead, limitations and next action without closing PERF-04, M5 or publishing a release
