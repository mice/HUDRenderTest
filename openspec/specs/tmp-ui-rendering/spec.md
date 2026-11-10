## Purpose

Provide TextMeshPro UI rendering support for the existing HUD batch renderer while preserving the legacy Text path.

## Requirements

### Requirement: TMP package availability
The project SHALL include TextMeshPro as an explicit Unity package dependency and SHALL include the resources needed to create and render TMP UI text in a clean checkout.

#### Scenario: Package dependency resolves
- **WHEN** Unity resolves packages for the project
- **THEN** `com.unity.textmeshpro` is available to runtime assemblies that implement TMP UI rendering

#### Scenario: TMP resources are present
- **WHEN** a TMP UI text component is created for tests or sample scenes
- **THEN** required TMP resources and default font/material assets are available without manual editor setup

### Requirement: Legacy Text compatibility
The project SHALL keep the existing legacy `UIText` rendering path functional while TMP support is developed.

#### Scenario: Existing legacy tests run
- **WHEN** existing Text-based EditMode and PlayMode tests are executed
- **THEN** they continue to pass without requiring TMP components

### Requirement: TMP text mesh generation
The TMP adapter SHALL generate `IUIData` mesh content from a supported `TextMeshProUGUI` component.

#### Scenario: Basic TMP text generates mesh data
- **WHEN** a supported TMP text component contains visible text
- **THEN** the adapter produces vertices, uv0 values, colors, and triangle indices suitable for the current batch renderer

#### Scenario: TMP text updates regenerate mesh data
- **WHEN** the text content is changed through the prefab holder text API
- **THEN** the stored mesh data is regenerated and reflects the new content

### Requirement: Low-level Text-equivalent rendering
For supported content, the TMP implementation SHALL reach visual parity with the legacy Text implementation for position, color tint, basic layout, and batch renderer output.

#### Scenario: TMP renders through batch renderer
- **WHEN** a holder containing TMP text is rendered by the HUD batch renderer
- **THEN** the text appears in the expected location with expected tint and mesh geometry

#### Scenario: TMP transform handling matches legacy Text
- **WHEN** TMP text is generated under a holder root with local transform offsets
- **THEN** generated mesh vertices are transformed consistently with the existing Text path

### Requirement: Advanced TMP rendering path
The project SHALL define a staged path for TMP-specific SDF rendering and outline support after low-level parity is complete.

#### Scenario: SDF rendering is explicitly selected
- **WHEN** the advanced TMP shader path is enabled for a supported TMP font asset
- **THEN** text alpha is derived from TMP SDF data rather than legacy alpha-font sampling

#### Scenario: TMP outline support is enabled
- **WHEN** a supported TMP text material has outline settings enabled
- **THEN** the advanced rendering path preserves visible outline behavior without relying on legacy `UIOutline`

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
