## ADDED Requirements

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
The UGUI extraction implementation SHALL use supported public APIs or package-owned abstractions and SHALL preserve the renderer's indexed mesh data contract without compile-time access to UGUI internal fields.

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
