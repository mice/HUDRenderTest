## 1. Baseline and Acceptance

- [x] 1.1 Run the current modified-UGUI EditMode and PlayMode suites and record test counts and artifacts as the behavioral baseline.
- [x] 1.2 Capture representative mesh-extraction CPU and managed-allocation measurements for `FillData2` and `FillData3` after buffer warm-up.
- [x] 1.3 Define and document the material CPU-regression threshold for accepting the portable extraction path from the captured baseline.
- [x] 1.4 Inventory the staged migration, ignored package dependencies, package/Sample assembly boundaries, and current package-lock sources.

## 2. Portable UGUI Extraction

- [x] 2.1 Add focused governed EditMode cases that lock down vertex channels, exact indexed topology, triangle order, offsets, and texture/flag packing.
- [x] 2.2 Replace direct `VertexHelper` field access with a package-owned adapter using public vertex and reusable mesh/index APIs.
- [x] 2.3 Reuse scratch objects and destination capacity so repeated extraction after warm-up has zero additional managed allocations.
- [x] 2.4 Add governed allocation and repeated-use tests for the public extraction path.
- [x] 2.5 Compare the public path with the recorded CPU baseline and stop for design review if it exceeds the accepted regression threshold.
- [x] 2.6 Verify that package runtime source no longer references UGUI internal fields or requires `InternalsVisibleTo("UGui.Extends")`.

## 3. Dependency Closure

- [x] 3.1 Remove the local embedded UGUI fork from the Sample dependency environment after the portable extraction path passes its gates.
- [x] 3.2 Resolve the declared stock UGUI dependency and regenerate `HUDRenderTestSample/Packages/packages-lock.json` without an absent embedded source.
- [x] 3.3 Add a validation check that rejects required ignored/untracked packages and embedded lock entries without a tracked package directory.
- [x] 3.4 Export a repository-tracked snapshot and prove package and Sample compilation without an existing Unity `Library`.
- [x] 3.5 Run all package EditMode tests against stock UGUI in the clean snapshot.

## 4. Package and TMP Asset Boundary

- [x] 4.1 Build a GUID/runtime-lookup inventory for files under `com.uidatarender/Runtime/TextMesh Pro` and their package/Sample consumers.
- [x] 4.2 Retain the minimal settings, font, material, atlas, attribution/license, and custom shader assets required by clean runtime and test contracts.
- [x] 4.3 Move sample-only TMP assets to `HUDRenderTestSample` and remove redundant standard TMP implementation assets supplied by the declared dependency, preserving required `.meta` identities.
- [x] 4.4 Verify that package runtime content has no serialized or code reference to `HUDRenderTestSample`.
- [x] 4.5 Run TMP EditMode coverage and the TMP PlayMode rendering scenario after the asset changes.

## 5. Test Governance and Documentation

- [x] 5.1 Update the governance synchronizer's Package/Sample classification so existing Mesh, UI, Scene, Job, and Diagnostic records are not regenerated under `Misc`.
- [x] 5.2 Enforce repository-wide unique governed test IDs, one record per test, one test per active record, and resolvable scope-relative test paths.
- [x] 5.3 Remove duplicate and orphan package/Sample records and regenerate both governance indexes.
- [x] 5.4 Run governance synchronization twice and verify that the second run produces no diff.
- [x] 5.5 Update `AGENTS.md`, repository/package README files, active test records, and automation documentation to use the migrated paths.
- [x] 5.6 Classify `Documentation~/AndroidPerformanceAutomationRequirements.md` as package documentation, Sample documentation, repository documentation, or intentionally excluded local material.
- [x] 5.7 Mark retained historical design documents as historical or update path references that are intended as current guidance.

## 6. Repository Validation and CI

- [x] 6.1 Add a package-layout validator modeled on the reference repository for metadata, local link, Unity version, dependency sources, asmdefs, runtime/test separation, required assets, and ownership boundaries.
- [x] 6.2 Add a clean-checkout validation entry point that reports compile/test artifacts and cannot reuse the working project's ignored packages or `Library`.
- [x] 6.3 Integrate structural validation with the maintained local validation workflow while preserving fast EditMode/PlayMode skip options.
- [x] 6.4 Update CI to run structure and clean-compilation checks, package EditMode tests, Sample PlayMode tests, and the `UIDataRender` coverage gate.
- [x] 6.5 Upload EditMode, PlayMode, clean-checkout, and coverage logs/results when CI succeeds or fails.

## 7. Final Verification

- [x] 7.1 Validate the OpenSpec change and resolve all specification-format or consistency errors.
- [x] 7.2 Run complete local EditMode and PlayMode validation with no skipped tests.
- [x] 7.3 Run managed coverage validation with the existing 70 percent `UIDataRender` threshold.
- [x] 7.4 Run the full structure and clean-checkout validation from repository-tracked state.
- [x] 7.5 Review Git status, package `.meta` completeness, ignored dependency paths, and the final staged diff for unintended generated output or asset changes.
- [x] 7.6 Record validation commands, test counts, coverage, performance comparison, and TMP scene results in the delivery summary.
