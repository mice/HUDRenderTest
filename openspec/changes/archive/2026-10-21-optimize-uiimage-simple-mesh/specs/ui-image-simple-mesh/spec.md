## ADDED Requirements

### Requirement: Safe standard UIImage fast path
The package SHALL provide an application-layer fast path for eligible standard `UIImage` sprites and SHALL keep the existing UGUI extraction path for every ineligible image.

#### Scenario: Eligible simple image uses direct geometry
- **WHEN** a non-null sprite uses the supported standard Simple Image configuration
- **THEN** `UIImage.DoGenerate` writes equivalent four-vertex/six-index geometry without invoking the UGUI extraction round trip

#### Scenario: Complex image uses fallback
- **WHEN** an image is Filled, Sliced, Tiled, custom, or otherwise outside the eligibility predicate
- **THEN** `UIImage.DoGenerate` uses the existing UGUI generation and extraction path

### Requirement: Geometry equivalence
The fast path SHALL preserve the fallback path's vertex positions, UV channels, colors, index topology, texture index, and parent-relative transform result within the existing test tolerance.

#### Scenario: Standard image output matches fallback
- **WHEN** the same eligible image is generated through the fast path and forced fallback
- **THEN** all mesh channels and indices compare equal within the defined tolerance

#### Scenario: Parent transform remains equivalent
- **WHEN** an eligible image is generated under a different parent transform
- **THEN** the transformed vertex output matches the fallback output within the defined tolerance

### Requirement: Observable and reversible optimization
The implementation SHALL expose separate profiler markers for fast-path hits and fallback generation, and SHALL allow the fast path to be disabled for diagnostic comparison without removing the fallback.

#### Scenario: Profiler distinguishes paths
- **WHEN** a supported image is generated
- **THEN** the profiler records the direct UIImage path marker

#### Scenario: Fast path disabled
- **WHEN** the diagnostic fast-path switch is disabled
- **THEN** all images use the existing UGUI fallback and produce the same output
