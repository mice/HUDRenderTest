---
testId: UT_MESH_035
module: Mesh
testType: EditMode
target: VertexHelperUtils
testFile: Tests/EditMode/TestVertexHelperUtils.cs
testMethod: StockCopyIndices_ReleaseDestroysScratchAndNextCallRecreatesIt
status: active
lastRun: ""
lastResult: unknown
---

# UT_MESH_035

## Requirement
VERIFY-02: explicitly releasing shared fallback resources destroys the native Mesh and permits correct recreation.

## Risk
Regression in VertexHelperUtils could break the documented Unity UIDataRender behavior.

## Steps
1. Run the EditMode test assembly containing `StockCopyIndices_ReleaseDestroysScratchAndNextCallRecreatesIt`.
2. Let the NUnit/Unity Test Runner execute the assertions.

## Assertions
See `Tests/EditMode/TestVertexHelperUtils.cs` / `StockCopyIndices_ReleaseDestroysScratchAndNextCallRecreatesIt`.

## Expected Result
The test passes without unexpected logs, leaks, or assertion failures.

## Latest Run Result
Passed on 2026-11-06 in both the custom Sample and actual BuiltIn UGUI 1.0.0 consumer: extraction 13/13 and complete EditMode 196/196 per environment, no failures or skips. See repository acceptance conclusions `Documentation~/verify-02-stock-ugui-results-2026-11-06.md`; raw XML/logs remain local. Frontmatter is maintained by the governance synchronizer.

## Notes
Release first, extract, inspect the uniquely named hidden Mesh, release twice, verify the old Mesh is destroyed immediately in EditMode, extract again and verify a different Mesh and correct indices. TearDown releases scratch even on assertion failure.

Explicit `VertexHelperUtils.CopyIndices` calls force the public-API fallback even when the installed UGUI has a custom instance method. The stock dependency's compiled assembly has no custom instance method; its recorded source hash matches the Editor's built-in package.
