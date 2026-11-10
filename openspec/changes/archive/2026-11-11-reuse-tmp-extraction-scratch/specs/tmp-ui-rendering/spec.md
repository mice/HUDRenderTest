## ADDED Requirements

### Requirement: Reusable TMP extraction scratch
The TMP adapter SHALL reuse main-thread scratch for warmed nonnested extraction within previously reached capacity, without managed allocation events attributable to scratch. Zero-event extraction SHALL apply with unchanged destination lengths for Managed storage or sufficient capacity for Slim storage. It SHALL preserve positions, UVs, colors, topology, texture index and flags for both storage modes, including changed and empty content.

#### Scenario: Warm fixed-capacity extraction
- **WHEN** fixed or changing text with stable output lengths is extracted after storage and scratch warmup within reached capacity
- **THEN** positive-controlled GC.Alloc measurement records zero extraction events
- **AND** generated channels and topology match the source TMP geometry

#### Scenario: Growing shrinking and empty content
- **WHEN** text grows, shrinks or becomes empty, including the first extraction after release
- **THEN** only current visible characters are emitted and no previous geometry survives

### Requirement: Safe TMP scratch lifecycle
The adapter SHALL provide idempotent main-thread scratch release and cleanup at subsystem registration, application quit and editor reload/quit. Nested extraction and callback exceptions SHALL preserve outer extraction and allow subsequent extraction.

#### Scenario: Release and recreation
- **WHEN** scratch is released repeatedly and then extraction resumes
- **THEN** valid output is produced from recreated scratch

#### Scenario: Nested extraction and cleanup during a callback
- **WHEN** a FillVertex callback starts nested extraction, requests release, or throws
- **THEN** active helpers remain valid until their extraction exits and subsequent extraction starts cleanly
