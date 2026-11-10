## ADDED Requirements

### Requirement: Representative repository resource fixture
The runner SHALL provide an opt-in deterministic fixture using three distinct repository Sprite textures and primary-atlas TMP text through production generation, merge and draw APIs. Historical defaults SHALL remain reproducible. Bucket snapshots SHALL include text state. Reports SHALL identify resource inventory, source/build identity and UGUI provenance.

#### Scenario: Repeated resource workload
- **WHEN** the resource profile runs with identical configuration using SingleBucket, SameFrame and Rotating scheduling
- **THEN** input requests and final geometry checksums SHALL agree, including text and texture slots, and reports SHALL distinguish this profile from the historical null-Sprite fixture

#### Scenario: Invalid or missing fixture
- **WHEN** required resources are missing, share an image texture, or exceed supported fixture bounds
- **THEN** the run SHALL fail before sampling instead of substituting synthetic geometry

### Requirement: Calibrated bounded allocation diagnostics
The runner SHALL support an opt-in current-thread GC.Alloc event diagnostic with a known allocation positive control, bounded storage and explicit saturation handling. Event bytes and event counts SHALL be distinct. Bytes SHALL additionally require a Bytes unit and a positive control of at least 4096 bytes; marker durations SHALL NOT be reported as allocated bytes. Unsupported or uncalibrated results SHALL be unavailable. IL2CPP SHALL never call GetAllocatedBytesForCurrentThread.

#### Scenario: Diagnostic counter available
- **WHEN** the recorder observes the known allocation and the measured scope does not saturate
- **THEN** the report SHALL include measured HUD event counts, include bytes only when separately calibrated, and identify diagnostic instrumentation and build type

#### Scenario: Counter missing or saturated
- **WHEN** calibration fails or storage saturates
- **THEN** affected metrics SHALL have availability false and sentinel -1 and SHALL NOT establish zero GC

### Requirement: Separate representative timing and diagnostic acceptance
Representative device evidence SHALL use repeated traceable Release timing runs with diagnostics disabled and separate allocation diagnostic runs where needed. Capture SHALL retain environment and thermal validity evidence. Existing budgets SHALL remain in force; incomplete or overbudget evidence SHALL leave PERF-04 open until its acceptance criteria are met or explicitly excepted.

#### Scenario: Representative load exceeds existing budget
- **WHEN** valid representative device measurements exceed the existing budget or required counters remain unavailable
- **THEN** the conclusion SHALL state the observed result and remaining gate without silently accepting a new budget
