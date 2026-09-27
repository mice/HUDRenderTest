## ADDED Requirements

### Requirement: Reproducible public UGUI benchmark

The package SHALL provide a repeatable Unity EditMode measurement for public `VertexHelper` extraction covering both `FillData2` and `FillData3` with documented fixture, warm-up, iteration count, timer, Unity version, and execution environment.

#### Scenario: Benchmark records both extraction paths
- **WHEN** the governed benchmark runs in the target Unity 2022.3 environment
- **THEN** it records separate CPU measurements for `FillData2` and `FillData3` and identifies the fixture and sample configuration.

#### Scenario: Benchmark is repeatable after warm-up
- **WHEN** the benchmark is run again with the same fixture and environment
- **THEN** the evidence reports comparable summary statistics and does not rely on a single unqualified timing sample.

### Requirement: Extraction correctness and allocation gate

The public extraction path SHALL preserve vertex channels, triangle order, packed metadata, and non-zero offsets, and SHALL produce no additional managed allocation after reusable scratch and destination buffers reach steady-state capacity.

#### Scenario: Indexed geometry remains equivalent
- **WHEN** a warmed `VertexHelper` containing indexed geometry is extracted with non-zero destination offsets
- **THEN** vertex channels, UV metadata, triangle order, and offset-adjusted indices match the expected contract.

#### Scenario: Warm extraction allocates no managed memory
- **WHEN** the same extraction is repeated after scratch and destination capacities are warmed
- **THEN** the measured managed allocation for the repeated operation is zero bytes.

### Requirement: Performance decision is evidence based

The repository SHALL compare current public extraction measurements with the archived baseline and documented reference gates, and SHALL record either a validated optimization or an explicit acceptance decision with a usage budget, residual risk, and follow-up trigger.

#### Scenario: Optimization meets the decision gates
- **WHEN** a targeted optimization preserves correctness and allocation behavior and meets the selected CPU gates
- **THEN** the implementation, tests, before/after measurements, and decision are recorded as the accepted result.

#### Scenario: Reference gates remain unmet
- **WHEN** stock UGUI compatibility prevents meeting the selected CPU gates without violating correctness or allocation requirements
- **THEN** the evidence records the measured regression, accepted usage budget, residual risk, and condition that reopens the investigation.
