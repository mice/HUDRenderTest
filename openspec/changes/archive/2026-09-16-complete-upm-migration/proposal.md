## Why

The repository has been physically reorganized into a UPM package and a sibling Unity sample project, but the staged state is not reproducible from a clean checkout. The package currently compiles only when an ignored, locally modified embedded UGUI package grants access to internal `VertexHelper` fields, so CI and downstream consumers cannot rely on the declared package dependencies alone.

## What Changes

- Complete the separation of reusable runtime, tests, documentation, and sample-only assets between `com.uidatarender` and `HUDRenderTestSample`.
- Remove the runtime dependency on an ignored UGUI source modification and make the package compile against its declared `com.unity.ugui` dependency using supported package-owned code.
- Preserve mesh extraction behavior and allocation-conscious rendering, with focused correctness and performance validation around the replacement UGUI extraction path.
- Make a clean checkout the authoritative validation environment for package dependency resolution and compilation.
- Add repository/package structure checks and run both package EditMode tests and sample PlayMode tests through maintained local and CI entry points.
- Reconcile package and sample test-governance records, remove duplicate/orphan records, and update generated paths for the new repository layout.
- Clarify TMP asset ownership so the package contains only resources required for its runtime contract while sample-only assets remain in the sample project.
- Update repository guidance and package documentation to describe the post-migration layout and workflows.

## Capabilities

### New Capabilities

- `upm-package-distribution`: Defines a self-contained, clean-checkout-installable `com.uidatarender` package with explicit dependencies and a controlled package/sample asset boundary.
- `repository-validation`: Defines structural, compilation, test, governance, and CI validation for the package repository and its sibling sample project.

### Modified Capabilities

None.

## Impact

- Runtime extraction code under `com.uidatarender/Runtime/UGUI.Extends` and its assembly references.
- Package metadata, Sample manifest/lock data, and any locally embedded UGUI dependency.
- TMP resources currently stored under `com.uidatarender/Runtime/TextMesh Pro` and the sample assets that reference them.
- EditMode and PlayMode test assemblies, governance records, and synchronization tooling.
- Local validation scripts, clean-clone validation, GitHub Actions, `AGENTS.md`, and repository/package README files.
- No intentional change to the public HUD rendering behavior, serialized sample scene behavior, or the existing legacy Text and TMP feature contracts.
