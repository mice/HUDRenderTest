# HUD 128-Holder Generation Performance Specification

## Purpose

Define the local 1.5 ms p95 regeneration target for the synchronous 128-holder workload on Mi 10 Release IL2CPP ARM64, including generation and merge/upload. Meeting this target does not establish overall RC or GC acceptance. The Burst/Job prototype was not implemented and was handed off to JOB-01 based on measurements of the retained synchronous path.

## Requirements

### Requirement: Synchronous 128-holder update budget
The optimized path SHALL update all 128 requested holders in the same frame and meet a p95 regeneration target of 1.5 ms on the specified Mi 10 Release IL2CPP ARM64 workload. Regeneration MUST include generation and merge/upload, including any data preparation, copying, scheduling and waiting introduced by the optimization.

#### Scenario: Full refresh acceptance
- **WHEN** the 128-holder workload refreshes all holders using the candidate path
- **THEN** every candidate acceptance run has full-refresh regeneration p95 at or below 1.5 ms and records the actual number of updated holders.

#### Scenario: Budget remains unmet
- **WHEN** measured p95 exceeds 1.5 ms or required evidence is unavailable
- **THEN** the change remains unaccepted and does not report success based on kernel-only timing or deferred work.

### Requirement: Equivalent generation and fallback
The optimized path SHALL preserve geometry, texture mapping, color, transforms and synchronous visibility for supported inputs and SHALL use the existing correct path for unsupported inputs.

#### Scenario: Supported dynamic inputs
- **WHEN** supported Simple images change dimensions, Sprite, color or parent transform
- **THEN** candidate and reference output match within documented floating-point tolerance, with identical topology, slots and color semantics.

#### Scenario: Unsupported input
- **WHEN** an image cannot use the optimized batch generator
- **THEN** it is generated through the compatible fallback without missing or stale output.

### Requirement: Reusable data and measured execution choice
The implementation SHALL reuse warmed buffers and SHALL justify changes to its execution strategy using end-to-end measurements. A Burst/Job prototype SHALL be optional when the retained synchronous path already meets this change's budget; any deferred experiment MUST be recorded explicitly. Burst eligibility MUST exclude managed object access from the compiled kernel.

#### Scenario: Execution comparison
- **WHEN** a Burst or SIMD candidate is evaluated
- **THEN** the comparison includes input preparation, conversion, scheduling, completion and output consumption, and verifies that the intended compiled path actually executed.

### Requirement: Explicit stable hierarchy batch lifetime
Query reuse SHALL be opt-in through a disposable per-image scope. The caller MUST keep the parent/Canvas hierarchy and the root transform unchanged until disposal and MUST start a fresh scope after changing them. Image dimensions, Sprite, color and child transforms SHALL remain live within a scope. Ordinary generation outside a scope SHALL retain existing behavior.

#### Scenario: Fresh batch after structural changes
- **WHEN** a previous scope is disposed and the hierarchy, Canvas availability or root transform changes before the next scope
- **THEN** the new scope captures current state and its generated output matches live generation.

#### Scenario: Scope ownership and different roots
- **WHEN** a caller attempts to nest scopes on the same image or disposes a copied older scope after starting a new scope
- **THEN** nesting is rejected, stale disposal does not close the new scope, and generation with a different root uses ordinary live queries.

### Requirement: Reproducible device evidence
Acceptance evidence SHALL preserve configuration, source and APK identity, raw reports and analysis. It MUST distinguish full-refresh frames from idle frames and unavailable counters from zero values.

#### Scenario: Matched device comparison
- **WHEN** an optimization is proposed for acceptance
- **THEN** at least four ABBA runs with at least 100 full refreshes per run compare matched workload and output, report median/p95/max per run, and record thermal state and allocation counter availability.
