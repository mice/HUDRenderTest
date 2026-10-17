# Historical quad write profiling results - 2026-10-17

This historical conclusion is retained from local commit `bbc0382`. It describes the older instrumentation and validation performed on 2026-10-17; it does not validate the current tree or a release candidate. The [later closure](evidence.md) records the subsequent opt-in measurement validity repair and repeated device comparisons, and remains authoritative for the current implementation. Raw reports, capture scripts and original source manifests are retained locally with the pre-rebase backup. Artifact paths below describe that original evidence set. The optimization proposal below is a historical proposal, not a new active task.

Xiaomi 10 (`791d02af`, Mi 10), 2026-10-17: three managed and three Slim runs completed with Release IL2CPP ARM64 Vulkan players. All 3,240 sampled frames passed timing, workload, geometry, source-fingerprint, and JSON/CSV consistency checks. The five substages are now populated; the earlier implementation exposed markers and fields but never assigned the timing fields.

## Configuration and provenance

- Base commit: `3a6f553ef3f03ed648aa6130e76ed16a80eb62b3`, with the profiling fixes in the working tree. Reports correctly identify a dirty source tree.
- Both builds use source fingerprint `6ABD6F15FF5407019755357FDBE5F0DCD8058CD7212085791C7A52DCD0FFA8D9`; its input hashes are saved in sources.sha256 (`evidence/sources.sha256`, local artifact). APK hashes and build settings are in builds.json (`evidence/builds.json`, local artifact).
- Native library inspection confirms `lib/arm64-v8a/libil2cpp.so`. AAPT confirms the ARM64 ABI and absence of `application-debuggable`: managed (`evidence/managed-apk-badging.txt`, local artifact), Slim (`evidence/slim-apk-badging.txt`, local artifact).
- `256` holders, `3` quads each, `10%` Partial updates, Burst every `30` frames, `60` warmup frames and `180` sample frames per workload, seed `12345`, target `60 FPS`. Every frame contains `3,072` vertices, `4,608` indices and one batch. Each run has six measured Burst refresh frames.
- The independent snapshot avoids closing the user's open Unity project. Its 449 tracked runtime/Assets files match the working tree after normalizing Git CRLF conversion, excluding the performance scene stamped by the build helper. The actual dependency manifest (`evidence/dependencies-manifest.json`, local artifact) and resolved lock (`evidence/dependencies-lock.json`, local artifact) are preserved. The user's separate agent-bridge upgrade in the live project's manifest was preserved; these builds use the snapshot's `v1.3.1` bridge.
- Application ID `com.DefaultCompany.HUDRenderTest.quadprofile` avoids the old test application's signing-key conflict. The old application and reports were retained. USB installation required enabling the phone's USB-install permission.
- Battery readings after each run are saved beside the reports. The first managed and final Slim readings were `25.2 C` and `25.7 C`; CPU frequency/governor and thermal throttling were not controlled.

## Burst refresh results

Values below are nearest-rank medians over six refresh frames per run, matching the runner's percentile convention. Shares divide each stage's median by the sum of the five stage medians; they are not shares of the aggregate write duration. All runs are included.

| Implementation/run | Aggregate write ms | Storage ms | Vertex ms | UV ms | Color ms | Index ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Managed 1 | 2.7749 | 0.2903 | 0.2259 | 0.2501 | 0.2417 | 0.2268 |
| Managed 2 | 1.3065 | 0.1305 | 0.1098 | 0.1123 | 0.1118 | 0.1071 |
| Managed 3 | 1.1461 | 0.1302 | 0.0976 | 0.1077 | 0.0989 | 0.0938 |
| Slim 1 | 1.3484 | 0.1146 | 0.1287 | 0.1241 | 0.1217 | 0.1214 |
| Slim 2 | 1.3537 | 0.1117 | 0.1265 | 0.1264 | 0.1211 | 0.1271 |
| Slim 3 | 1.3677 | 0.1122 | 0.1275 | 0.1223 | 0.1213 | 0.1243 |

| Implementation | Storage share | Vertex share | UV share | Color share | Index share |
| --- | ---: | ---: | ---: | ---: | ---: |
| Managed, range across runs | 22.8-24.6% | 18.3-19.2% | 19.7-20.4% | 18.7-19.6% | 17.8-18.7% |
| Slim, range across runs | 18.2-18.8% | 20.6-21.1% | 20.1-20.6% | 19.8-20.0% | 19.9-20.7% |

Raw JSON/CSV pairs are in managed (`evidence/managed`, local artifact) and slim (`evidence/slim`, local artifact). analysis.json (`evidence/analysis.json`, local artifact) contains each run's full medians, shares and filename. Run the adjacent Analyze-Reports.ps1 (`evidence/Analyze-Reports.ps1`, local artifact) to reproduce the checks and summary.

## Interpretation and next optimization

Managed storage checks have the largest measured substage share, but none of the five stages dominates. Slim's stages are nearly evenly distributed. A single vertex/UV/color/index micro-optimization is therefore unlikely to remove most of the observed write cost.

The median of the per-frame substage sum is only about 44-46% of the aggregate write median in these runs. The remainder includes timestamp reads between stages, timing conversions/assignments, diagnostics dispatch and surrounding bookkeeping. Per-stage timer/profiler costs also occur inside measured intervals. This experiment has no timer-only calibration or diagnostics-disabled A/B run, so that remainder cannot be attributed solely to instrumentation, and these numbers are diagnostic estimates rather than pure array-write costs.

Managed aggregate write medians varied from `2.7749` to `1.1461 ms` despite identical inputs. The six refresh samples per run, sequential run order and uncontrolled CPU frequency prevent a reliable managed-versus-Slim speed ranking. The older null-texture baseline also used target frame rate `-1`, so no end-to-end speedup is claimed against it.

The next bounded optimization candidate is a managed warm-storage fast path: cache ready array references and avoid repeating storage validation on an already canonical quad, while preserving reallocation and geometry behavior. Storage accounts for only roughly 10-11% of the measured aggregate in these runs, so even removing that measured stage would have limited impact. Validate any candidate with diagnostics disabled and repeated/interleaved baseline/candidate runs before accepting a performance claim. If that gain is negligible, prioritize reducing repeated per-quad dispatch with a batched writer instead of optimizing one attribute stream. Neither optimization is implemented by this profiling change.

## Validation

- `Invoke-LocalValidation.ps1 -ProjectPath <snapshot>`: EditMode `137/137`, PlayMode `26/26` passed. The final same-instance enabled-to-disabled timing reset assertions were then rerun with `-SkipPlayMode`: `137/137` passed. Saved NUnit evidence: EditMode (`evidence/editmode-results.xml`, local artifact), PlayMode (`evidence/playmode-results.xml`, local artifact).
- The two governed geometry tests verify unchanged canonical geometry with diagnostics on and off, non-negative stage values, a positive enabled total and cleared timings on the same instance after disabling diagnostics. `BuildMeshDiagnostics.Enabled` is restored in `finally`; Slim geometry is disposed in `finally`.
- No new heap allocation was added to `WriteSimpleQuad`'s diagnostics: timings are a value struct and timestamps use static Stopwatch APIs. Existing cold-storage allocations remain. IL2CPP scoped allocation counters are unavailable, so no device-side zero-allocation claim is made.
- Package/layout and its governance gate passed. `openspec validate profile-simple-quad-write --strict` passed.
- The clean-export wrapper initially stopped because `packages-lock.json` is not tracked. Validation used the independent exported snapshot after copying the live dependency lock, through the normal local-validation script; this is not claimed as a passing clean-export gate.
- Both APKs compiled successfully after adding the build mode, target frame rate, optional application ID and independent project-path parameters. Build settings are restored in `finally`.
