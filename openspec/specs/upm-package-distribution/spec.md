# UPM Package Distribution Specification

## Purpose

Define the package layout, dependency closure, portable UGUI extraction, asset ownership, and TMP resource requirements for distributing `com.uidatarender` independently.

## Requirements

### Requirement: UPM repository layout
The repository SHALL provide `com.uidatarender` as a Unity Package Manager package and `HUDRenderTestSample` as a sibling Unity project that consumes the package through a local file dependency.

#### Scenario: Sample resolves the sibling package
- **WHEN** Unity opens `HUDRenderTestSample` from a clean checkout
- **THEN** the `com.uidatarender` manifest entry resolves to the repository's sibling package directory

#### Scenario: Package metadata is valid
- **WHEN** package structure validation reads `com.uidatarender/package.json`
- **THEN** the package has a valid name, semantic version, Unity baseline, description, and explicit runtime dependencies

### Requirement: Declared dependency closure
The package MUST compile using only repository-tracked files, Unity built-in modules, and dependencies declared in package metadata and the consuming project manifest.

#### Scenario: Stock UGUI compiles the package
- **WHEN** a clean project resolves the declared `com.unity.ugui` version without local source modifications
- **THEN** every `com.uidatarender` runtime assembly compiles successfully

#### Scenario: Ignored dependency cannot mask a missing declaration
- **WHEN** structural validation finds a package required for compilation only inside an ignored or untracked directory
- **THEN** validation fails and identifies the undeclared dependency

#### Scenario: Lock state matches available sources
- **WHEN** the Sample dependency lock is validated in a clean checkout
- **THEN** no dependency is marked embedded unless its complete embedded package is tracked at the recorded path

### Requirement: Portable UGUI mesh extraction
The UGUI extraction implementation SHALL use supported public APIs or package-owned abstractions and SHALL preserve the renderer's indexed mesh data contract without compile-time access to UGUI internal fields. Stock UGUI SHALL use a package-owned fallback. A compatible public custom UGUI instance method SHALL remain usable as an optional acceleration path.

#### Scenario: Vertex channels are preserved
- **WHEN** a `VertexHelper` containing positions, colors, UV values, and triangles is extracted
- **THEN** the destination buffers contain equivalent vertex channels and triangle order

#### Scenario: Renderer metadata is packed
- **WHEN** extraction is requested with a texture index and flags
- **THEN** each output UV stores the requested texture index and flags in the renderer-defined channels

#### Scenario: Offsets are applied
- **WHEN** geometry is appended at non-zero vertex and index offsets
- **THEN** output data is written at those offsets and triangle indices include the vertex offset

#### Scenario: Steady-state extraction avoids managed allocation
- **WHEN** extraction repeats after scratch and destination buffers have reached sufficient capacity
- **THEN** the extraction operation produces no additional managed allocations

#### Scenario: Equal vertex attributes retain indexed identity
- **WHEN** stock fallback extracts valid triangles referencing distinct vertices with equal attributes and a helper containing unused vertices
- **THEN** triangle order and vertex indices are preserved without deduplication or renumbering

#### Scenario: Invalid destination is rejected before writing
- **WHEN** fallback receives a null destination or a negative, insufficient, or overflow-sized destination offset
- **THEN** it throws the corresponding argument exception without changing destination contents

#### Scenario: Empty extraction after populated extraction
- **WHEN** fallback extracts a helper with no indices after previously extracting populated geometry
- **THEN** it writes no stale indices and leaves the destination unchanged

#### Scenario: Stock FillMesh boundary is explicit
- **WHEN** nonempty fallback extraction receives at least 65,000 vertices
- **THEN** it fails explicitly under the documented stock UGUI FillMesh limit without writing truncated indices

### Requirement: Extraction scratch lifecycle
Package-owned extraction scratch resources SHALL be reusable on the Unity main thread and SHALL support explicit release and automatic cleanup on editor assembly reload, editor exit, runtime subsystem reset, and application quit.

#### Scenario: Scratch is released and recreated
- **WHEN** scratch has been used, explicitly released, and extraction is requested again
- **THEN** the previous Mesh is released using the appropriate Unity destruction lifecycle and the new extraction returns correct indices

### Requirement: Stock dependency validation evidence
Distribution acceptance SHALL include compilation and extraction correctness/allocation results from a clean consumer resolving declared stock UGUI without local source modifications. Evidence SHALL identify the package source snapshot and resolved dependency provenance.

#### Scenario: Custom override cannot establish stock acceptance
- **WHEN** the original Sample uses the custom local UGUI
- **THEN** those results establish custom-path regression status only and a separate stock resolution is required for VERIFY-02 closure

#### Scenario: Missing or empty results fail validation
- **WHEN** a stock validation run produces no NUnit XML, zero executed tests, or failing tests
- **THEN** validation fails and VERIFY-02 remains open

### Requirement: Package and Sample asset ownership
Reusable runtime and deterministic package-test content SHALL reside in `com.uidatarender`, while scenes, demo drivers, performance workflows, and sample-specific assets SHALL reside in `HUDRenderTestSample`.

#### Scenario: Runtime package is independent of Sample assets
- **WHEN** `com.uidatarender` is installed into a different Unity project
- **THEN** package runtime assemblies and required runtime resources load without references to `HUDRenderTestSample`

#### Scenario: Sample content remains functional
- **WHEN** the sibling Sample project opens after migration
- **THEN** its scenes, prefabs, art, drivers, and PlayMode tests retain valid serialized references

### Requirement: Minimal clean TMP resources
The package SHALL provide the TMP resources required by its documented runtime and clean-test contract without redundantly shipping sample-only content or standard TMP implementation assets supplied by `com.unity.textmeshpro`.

#### Scenario: Clean TMP test setup works
- **WHEN** TMP package tests or supported Sample scenes run in a clean checkout
- **THEN** required settings, font, material, atlas, and custom renderer shader resources are available without a manual editor import step

#### Scenario: TMP asset inventory is justified
- **WHEN** package asset validation examines bundled TMP files
- **THEN** each retained file is referenced by serialized package content, runtime lookup, documentation/license obligation, or a clean-test requirement
