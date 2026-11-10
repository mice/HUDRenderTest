---
testId: UT_MESH_032
module: Mesh
testType: EditMode
target: VertexHelperUtils
testFile: Tests/EditMode/TestVertexHelperUtils.cs
testMethod: StockCopyIndices_RejectsInvalidRangesBeforeWriting
status: active
lastRun: ""
lastResult: unknown
---

# UT_MESH_032

## Requirement
VERIFY-02: invalid fallback destinations fail before writing, including offsets that would overflow an additive range check.

## Risk
Regression in VertexHelperUtils could break the documented Unity UIDataRender behavior.

## Steps
1. Run the EditMode test assembly containing `StockCopyIndices_RejectsInvalidRangesBeforeWriting`.
2. Let the NUnit/Unity Test Runner execute the assertions.

## Assertions
See `Tests/EditMode/TestVertexHelperUtils.cs` / `StockCopyIndices_RejectsInvalidRangesBeforeWriting`.

## Expected Result
The test passes without unexpected logs, leaks, or assertion failures.

## Latest Run Result
Passed on 2026-11-06 in both the custom Sample and actual BuiltIn UGUI 1.0.0 consumer: extraction 13/13 and complete EditMode 196/196 per environment, no failures or skips. See repository acceptance conclusions `Documentation~/verify-02-stock-ugui-results-2026-11-06.md`; raw XML/logs remain local. Frontmatter is maintained by the governance synchronizer.

## Notes
Cover null helper, null destination, insufficient capacity, negative offset, offset one past capacity and int.MaxValue. Assert the original destination sentinels remain intact.

Explicit `VertexHelperUtils.CopyIndices` calls force the public-API fallback even when the installed UGUI has a custom instance method. The stock dependency's compiled assembly has no custom instance method; its recorded source hash matches the Editor's built-in package.
