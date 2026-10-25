---
testId: UT_PERF_017
module: Performance
testType: PlayMode
target: Scene perf runners
testFile: Assets/Tests/PlayMode/TestHudRcPerformanceRunner.cs
testMethod: SpriteComparison_UsesSelectedPath_AndRestoresSwitchAndResources
status: active
lastRun: ""
lastResult: unknown
---

# UT_PERF_017

## Requirement
Sprite comparison runs select the requested path and restore global state and owned resources.

## Risk
A report could claim a Sprite comparison while measuring fallback/null-Sprite, leak a Sprite, or leave the global switch changed.

## Steps
Run Managed/Slim × direct/fallback, verify prepared path counts/configuration and mesh workload; ensure completion restores the switch and destroys generated Sprite/objects.

## Assertions
See the governed test method; no timing threshold is asserted.

## Expected Result
All output, state and lifetime assertions pass.

## Latest Run Result
2026-10-20: passed in full EditMode 147/147 and PlayMode 28/28 regression.

## Notes
SourceEvidence: openspec/changes/archive/2026-10-21-optimize-uiimage-simple-mesh/evidence.md
