# Simple Quad Write Profiling

## Purpose

Define opt-in direct quad write diagnostics for managed and Slim mesh data, including measurement validity, allocation behavior, and restoration of diagnostic settings.

## Requirements

### Requirement: Report direct quad write substages

The direct quad path SHALL expose allocation-free opt-in timings for storage, vertex, UV, color, and index writes for managed and Slim mesh data.

#### Scenario: Diagnostics capture write stages
- **WHEN** the HUD RC runner writes a direct quad with diagnostics enabled
- **THEN** the report includes non-negative substage timings and preserves the aggregate write timing.
- **AND** it records the measured quad count, timing configuration and timestamp frequency so sampled zero durations can be distinguished from unavailable measurements.

#### Scenario: Write timing is disabled or incomplete
- **WHEN** quad timing is disabled, no quad is written, or fewer quads are measured than the workload expects
- **THEN** the report marks write timings unavailable using an explicit validity flag, JSON -1 values and CSV unavailable fields, without treating default zeros as measured cost.

#### Scenario: Disabled diagnostics preserve behavior
- **WHEN** a normal caller writes a direct quad with diagnostics disabled
- **THEN** geometry output and public APIs remain unchanged.
- **AND** previous diagnostic results are cleared and repeated warmed writes introduce no managed allocation.

#### Scenario: Runner releases diagnostic settings
- **WHEN** the HUD RC run completes, fails or is reset
- **THEN** the previous diagnostic settings are restored along with the runner's other global settings.
