## ADDED Requirements

### Requirement: Report direct quad write substages

The direct quad path SHALL expose allocation-free opt-in timings for storage, vertex, UV, color, and index writes for managed and Slim mesh data.

#### Scenario: Diagnostics capture write stages
- **WHEN** the HUD RC runner writes a direct quad with diagnostics enabled
- **THEN** the report includes non-negative substage timings and preserves the aggregate write timing.

#### Scenario: Disabled diagnostics preserve behavior
- **WHEN** a normal caller writes a direct quad with diagnostics disabled
- **THEN** geometry output and public APIs remain unchanged.
