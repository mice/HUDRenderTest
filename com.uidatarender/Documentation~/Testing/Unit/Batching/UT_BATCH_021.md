---
testId: UT_BATCH_021
module: Batching
testType: EditMode
target: Holder batching and scheduling
testFile: Tests/EditMode/TestHolderBucketScheduler.cs
testMethod: FailedReplacement_KeepsOldEpochAndOutput_ThenSuccessfulReplacementCancelsRequests
status: active
lastRun: ""
lastResult: unknown
---

# UT_BATCH_021

## Requirement
FailedReplacement KeepsOldEpochAndOutput ThenSuccessfulReplacementCancelsRequests.

## Risk
A failed staging operation could destroy visible output or incorrectly acknowledge pending requests.

## Steps
Inject a binding or replacement generation failure, inspect old visible mesh and handle versions, then recover explicitly.

## Assertions
Old output and pending requests survive failure; successful recovery publishes complete latest geometry and releases replaced resources.

## Expected Result
Atomic publication and epoch ownership hold across failures.

## Latest Run Result
2026-10-25: passed; full regression 161/161 EditMode and 31/31 graphical PlayMode. Evidence: openspec/changes/add-holder-bucket-scheduling/evidence/local-validation.json.

## Notes
SourceEvidence: openspec/changes/add-holder-bucket-scheduling/specs/holder-bucket-scheduling/spec.md
