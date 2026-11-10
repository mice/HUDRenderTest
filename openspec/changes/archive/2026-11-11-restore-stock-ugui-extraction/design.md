## Context

The package declares UGUI 1.0.0. The Sample instead resolves the repository's tracked custom local UGUI containing `VertexHelper.CopyIndices`, which masks a compile-time dependency. VERIFY-02 belongs to M5 and targets unreleased v0.1.2. The original project's dependency files already contain user changes and must be preserved.

## Goals / Non-Goals

**Goals:** Restore stock compilation, exact indexed extraction and warmed zero managed allocation; retain optional custom acceleration; release fallback resources; validate real stock provenance.

**Non-Goals:** New renderer features, changes to the historical PERF-01 benchmark or device conclusions, package version changes, or release publication.

## Decisions

1. Add `VertexHelperUtils.CopyIndices` as a public extension with the existing method's signature. Rename the private profiling wrapper. C# gives an applicable instance method precedence over extensions: custom UGUI retains its current path, while stock compilation selects the package fallback. Explicit static calls exercise the fallback in either environment. No reflection, private fields, or dependency-specific defines are needed.
2. Extract through a lazily created shared Mesh and reusable `List<int>` using `FillMesh` and `GetTriangles`. Copy original indices in order with destination and vertex offsets. Stream deduplication was rejected because equal vertex attributes can have distinct indexed identities. Validate destination ranges with subtraction to avoid integer overflow. Empty streams write nothing.
3. Expose `ReleaseIndexScratch` for explicit cleanup. Release Mesh and managed capacity on editor assembly reload/editor exit, runtime subsystem registration, and application quit. Destroy immediately outside Play Mode and use Unity's deferred destruction while playing. The APIs are main-thread-only, matching Mesh/VertexHelper usage.
4. Use the existing clean validation pipeline with a preparation-only option, one reusable owned detached worktree under `D:/workspace/`, stock UGUI pinned in the exported Sample manifest, and recorded hashes/resolved lock provenance. Overlay trackable working-tree content and exclude the tracked custom UGUI using a temporary Git index. This includes the uncommitted package changes without changing the source index, branches, shared history or repository configuration. Remove optional agent tooling dependencies only in the snapshot. Preserve the source project's manifest and lock. Preparation must not launch Unity. Validation must reject missing, empty or skipped NUnit results. A local overridden Sample and actual stock consumer are both required.

## Risks / Trade-offs

- Stock fallback copies vertex data through a Mesh as well as reading indices; it has extra native work. Warmed allocation tests establish allocation behavior only. Historical custom-path timing results do not establish stock timing.
- `FillMesh` rejects helpers with 65,000 or more vertices when index extraction is required. Preserve that explicit failure instead of truncating/remapping geometry; test the boundary.
- Scratch capacity grows with the largest helper and remains until cleanup. Tests must clean up even after assertions fail.
- A Sample using custom UGUI cannot prove stock compilation. The clean snapshot must resolve UGUI 1.0.0 without a local override, record its actual source, and run extraction tests with nonzero results.
- A plain export outside the source repository has no Git provenance, making the Sample's report integrity test fail. Use a real detached worktree with the original HEAD and dirty transformed sources, retaining the byte fingerprint. Upgrade the owned initial export in place and preserve its Library; never relax the report assertion or copy fake commit metadata.
- Unity launch requires explicit permission (`UNITY_AI_EXCLUSIVE=false`); implementation and static checks precede that request. No MCP validation tools are available in the current session.

## Migration Plan

No consumer API migration is required. Install the updated package using the declared dependencies. Hosts unloading rendering resources can call `ReleaseIndexScratch`; the next fallback call recreates them. Rollback consists of reverting this change, with the original custom-UGUI dependency limitation restored.

## Open Questions

Resolved on 2026-11-06 after editor launch authorization: both dependency environments passed 13 extraction tests, 196 complete EditMode tests and 61 D3D11 PlayMode tests. Actual stock provenance and the initial plain-export report failure/fix are recorded in [VERIFY-02 acceptance](../../../../Documentation~/verify-02-stock-ugui-results-2026-11-06.md). Performance-budget and release acceptance remain with their existing roadmap nodes.
