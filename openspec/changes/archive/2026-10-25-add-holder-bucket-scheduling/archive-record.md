# Archive record — 2026-10-25

`add-holder-bucket-scheduling` is archived with all 10 tasks complete. Implementation and device evidence were committed as `8f33758`.

The 6 requirements and 17 scenarios were synchronized to the [main specification](../../../specs/holder-bucket-scheduling/spec.md). Archive-relative links and the capture script's repository-root lookup were adjusted for the new directory depth; raw reports, source manifests and test results were preserved.

Validation evidence: 161/161 EditMode, 31/31 graphical PlayMode, and seven fully analyzed Mi 10 captures. Rotating update CPU p95 is 1.3555 / 1.3413 ms, meeting this change's local 1.5 ms p95 target. The 5.3688 ms peak, increased two-frame total work and unavailable device allocation counters remain documented in the [results](evidence/bucket-results.md).

PERF-04/M5 remain open for the original RC budget, allocation counters, representative resource workloads and release validation. Archival does not close those broader requirements or constitute a release.
