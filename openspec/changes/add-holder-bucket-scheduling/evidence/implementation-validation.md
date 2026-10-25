# Bucket implementation validation — 2026-10-25

Local regression: **161/161 EditMode**, **31/31 graphical PlayMode**, zero skips. The graphical run used Direct3D 11 on NVIDIA GeForce RTX 3050 Ti. Exact tested source hashes are in [local-validation.json](local-validation.json); NUnit evidence is [EditMode](bucket-editmode.xml) and [PlayMode](bucket-playmode-graphics.xml).

| Contract | Evidence |
|---|---|
| Stable 1/128/129/256 membership boundaries; duplicate, foreign and stale handles rejected | UT_BATCH_011 |
| Latest value, no generation during Submit, first-pending age, one-frame fair rotation, pending position stays invisible | UT_BATCH_012 |
| Tick cutoff, duplicate Tick, backwards frames and reentrant mutation | UT_BATCH_013 |
| Generation failure preserves front and pending versions; full recovery | UT_BATCH_014 |
| Later bucket failure cannot publish an earlier bucket during a structural barrier | UT_BATCH_015 |
| Resource version changes prevent drawing and require explicit resynchronization | UT_BATCH_016 |
| Contiguous A/B/A batching, unchanged first-fit default, oversized holder failure invalidates plan cache | UT_BATCH_017 |
| Warm Submit/Tick/generate/fill/upload without managed GC.Alloc events, with positive control | UT_BATCH_018 |
| All three strategies converge to identical geometry; front/back object counts and external holder ownership | UT_BATCH_019 |
| Failure after upload during binding cannot publish or acknowledge a request | UT_BATCH_020 |
| Failed replacement keeps old epoch/output; successful replacement cancels old requests and releases owned output | UT_BATCH_021 |
| Actual production shader pixels across holder 128/129, changed bounds, single/multiple texture batches, publication revision refresh, camera command attachment/disposal | UT_PERF_019 |
| Real shared UIImage templates in three-strategy workload; matched request versions/frames, final geometry hashes, bounded drain, second refresh frame included, export parity | UT_PERF_020 |

Package tests: [TestHolderBucketScheduler.cs](../../../../com.uidatarender/Tests/EditMode/TestHolderBucketScheduler.cs). Sample tests: [TestHolderBucketRendering.cs](../../../../HUDRenderTestSample/Assets/Tests/PlayMode/TestHolderBucketRendering.cs), [TestHudRcPerformanceRunner.cs](../../../../HUDRenderTestSample/Assets/Tests/PlayMode/TestHudRcPerformanceRunner.cs).

The allocation assertion covers the warmed scheduler fixture on the Editor main thread. It does not establish whole-application or Android zero-GC acceptance. A two-frame rotation permits one extra frame of visible latency and coalesces intermediate values; it does not guarantee that every intermediate state was displayed.

Device performance is a separate gate. [capture-buckets.ps1](capture-buckets.ps1) runs the balanced six-capture comparison plus a legacy drawing control; [analyze-buckets.py](analyze-buckets.py) verifies source/APK/report hashes, request sequences, actual Unity-frame mapping, final output hashes, CSV parity and complete two-frame CPU windows. All seven captures passed full analysis; see [device results](bucket-results.md) and [summary](bucket-summary.json).
