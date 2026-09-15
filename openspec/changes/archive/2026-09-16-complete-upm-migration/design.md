## Context

The repository is in a staged migration from a single Unity project to a UPM repository containing `com.uidatarender` and the sibling `HUDRenderTestSample` project. Runtime code, EditMode tests, package metadata, scenes, sample drivers, and most documentation have already been moved with their Unity `.meta` files intact. The current working tree passes 116 EditMode and 22 PlayMode tests.

That success depends on `HUDRenderTestSample/Packages/com.unity.ugui@1.0.0`, an ignored embedded copy of UGUI whose `AssemblyInfo.cs` grants `InternalsVisibleTo("UGui.Extends")`. A repository-tracked clean snapshot resolves stock UGUI and fails compilation because `VertexHelperUtils` accesses `m_Positions`, `m_Colors`, `m_Uv0S`, and `m_Indices`. The migration is therefore structurally advanced but not reproducible or distributable.

The renderer is allocation-sensitive. Any portable replacement for direct UGUI list access must preserve indexed geometry, packed texture/flag UV values, vertex offsets, and steady-state allocation behavior. The existing TMP rendering specification also requires clean-checkout resources, while the current package contains a nearly complete TMP Essentials import that dominates package size and overlaps the declared TMP dependency.

## Goals / Non-Goals

**Goals:**

- Make `com.uidatarender` compile and test from a clean checkout using only repository-tracked files and declared package dependencies.
- Preserve the existing public rendering behavior and indexed mesh semantics while removing compile-time access to UGUI internals.
- Keep mesh extraction allocation-conscious, with no additional managed allocations after buffers have reached steady-state capacity.
- Establish explicit ownership for runtime package assets, sample-only assets, tests, and governance records.
- Add deterministic structural, clean-checkout, EditMode, PlayMode, coverage, and CI validation.
- Leave the repository documentation and agent guidance consistent with the new layout.

**Non-Goals:**

- Redesign the HUD batching architecture or public prefab/rendering API.
- Remove either the legacy `UIText` path or the existing `UITMPText` path.
- Change supported TMP features, rendering appearance, scene content, or performance-scene intent.
- Publish a package version or push repository changes as part of implementation.
- Upgrade Unity, Burst, TMP, or UGUI beyond the versions already selected for this migration.

## Decisions

### 1. Stock UGUI is the package compatibility boundary

`com.uidatarender` will compile against its declared stock `com.unity.ugui` dependency. Runtime code will not require an ignored embedded package, a modified `InternalsVisibleTo` declaration, or a consumer-side source patch.

Alternatives considered:

- Publishing a UGUI fork under the official package identifier preserves the current fast path but forces every consumer to replace a foundational Unity package and creates a hidden integration constraint.
- Reflecting private fields avoids compile-time access but remains coupled to undocumented field names and introduces IL2CPP/linker and versioning risk.

Both alternatives are rejected for the distributable package.

### 2. Preserve indexed geometry through public APIs

The replacement extraction adapter will use public UGUI and Unity mesh APIs. The preferred path is to read vertex channels with `VertexHelper.PopulateUIVertex` and obtain triangle indices through a reusable scratch `Mesh` populated by `VertexHelper.FillMesh`. Reusable arrays/lists and scratch state will avoid capacity-stable managed allocations. Tests will continue to assert vertex/index counts, channel values, offsets, and triangle order.

`VertexHelper.GetUIVertexStream` is not the primary path because it expands indexed geometry into triangle-stream vertices, increasing a normal quad from four to six vertices and changing the renderer's existing data contract.

Before removing the embedded fork, the implementation will capture correctness, allocation, and CPU baselines from the current path. The portable path must add no steady-state managed allocations. Median and tail CPU deltas will be recorded; a material regression will be reviewed before accepting the implementation rather than silently trading away renderer performance.

### 3. Clean tracked state is authoritative

Local validation of the populated working project remains useful for fast feedback, but release readiness will be determined from a clean repository-tracked snapshot. Structural validation will fail when a required dependency exists only in an ignored directory, when the Sample local package link resolves incorrectly, or when package metadata and lock state disagree.

The Sample lock file will be regenerated after the embedded UGUI copy is removed. Stock UGUI must resolve from the declared source rather than an absent embedded path.

### 4. Package and Sample retain separate ownership boundaries

- `com.uidatarender/Runtime` owns reusable renderer code, custom runtime shaders, and only the TMP resources required to satisfy the renderer's clean-install contract.
- `com.uidatarender/Tests/EditMode` owns deterministic package tests.
- `HUDRenderTestSample/Assets` owns scenes, art, demo/performance drivers, editor-only sample helpers, and sample-specific assets.
- `HUDRenderTestSample/Assets/Tests/PlayMode` owns scene and runtime integration tests.
- Each scope owns its matching `Documentation~/Testing/Unit` records.

The TMP asset set will be audited by serialized GUID and runtime lookup usage. Required default settings/font/material/atlas assets remain package-owned when needed for clean tests and runtime defaults. Imported TMP documentation, source fonts, emoji examples, and duplicate standard shaders will not remain in package Runtime unless a concrete serialized or runtime dependency requires them; standard implementation and shader assets should come from `com.unity.textmeshpro`.

### 5. Validation mirrors the repository boundary

A package-layout validator modeled after the reference repository will check package identity/version/Unity baseline, the Sample local link, dependency sources, asmdef uniqueness and runtime/test separation, required assets, test-governance consistency, and the absence of package references to Sample content.

The maintained validation flow will run package EditMode tests and Sample PlayMode tests. CI will execute both from a clean checkout, publish result/log artifacts, and retain the existing `UIDataRender` coverage gate. The clean-checkout path must not rely on a previously populated `Library` directory.

### 6. Governance is generated and idempotent

The governance synchronizer will understand Package and Sample scopes without creating `Misc` duplicates for already categorized tests. Across the repository, every governed test ID will be unique, every test will point to exactly one existing record, every record will point to an existing test, and a second synchronization run will produce no diff.

Legacy design documents may describe historical paths when clearly labeled as historical. Active instructions, generated test records, README files, and automation must use current package/Sample paths.

## Risks / Trade-offs

- **Risk: Public UGUI extraction is slower than direct list access.** -> Capture the current baseline first, reuse scratch storage, profile representative meshes, and stop for review if the CPU regression is material.
- **Risk: `FillMesh` introduces native work even when managed GC stays at zero.** -> Isolate index extraction, benchmark median and tail cost, and keep the adapter replaceable behind the existing extension-method contract.
- **Risk: Removing TMP Essentials files breaks serialized font/material references.** -> Build a GUID usage inventory before moving anything and validate both TMP EditMode tests and the TMP PlayMode scene after each asset-boundary change.
- **Risk: Large Unity asset moves obscure meaningful code changes.** -> Keep implementation scoped to the staged migration and review package, Sample, tooling, and documentation changes separately.
- **Risk: Clean-checkout tests are slower than working-tree tests.** -> Keep fast local validation while making clean validation mandatory for delivery and CI.

## Migration Plan

1. Record the current modified-UGUI correctness, allocation, and CPU baselines.
2. Implement and test the public-API extraction adapter while the old local environment remains available for comparison.
3. Remove the embedded UGUI dependency, regenerate package resolution state, and prove compilation in a clean tracked snapshot.
4. Audit and reduce TMP/package assets without breaking serialized references or clean resource availability.
5. Reconcile governance records and update active documentation and repository guidance.
6. Add package-layout and clean-checkout validation, then enable EditMode, PlayMode, and coverage checks in CI.
7. Run the complete clean validation suite and review the final staged diff before committing.

Rollback is repository-local: if the public extraction path fails correctness or performance review, do not release the package migration. Revert the implementation portion of this change or revise the extraction design; the ignored UGUI fork is not an acceptable released fallback.

## Open Questions

- What measured CPU regression threshold, in addition to zero steady-state managed allocations, should block acceptance of the public UGUI extraction path? The implementation task will record the current baseline before setting this threshold.
