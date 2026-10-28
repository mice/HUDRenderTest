# Damage text renderer comparison

This experiment compares shared-Quad character instancing (A) with persistent damage-text meshes (B). Both use the same event admission, cached layout, ordered bucket history, animation include, atlas, camera uniforms and blend/depth policy. A is not business ECS. Float32 remains the default.

## Sample

Open `HUDRenderTestSample/Assets/Scenes/Scene_DamageTextComparison.unity`. The baseline playback scene remains available separately. The comparison scene references the original fixed digit atlas/styles and nine new, recorded Clip assets in `Assets/DamageTextComparison`:

| Clip | Events | Use |
| --- | ---: | --- |
| `Static128_2/5/8` | 128 each | 2/5/8 digit draw-only checkpoints |
| `Static128_8_HighOverlap` | 128 | Same content/kinds/seeds, concentrated anchors |
| `Continuous60s` | 7,680 | Birth/expiry/upload lifecycle |
| `Bursts127/128/129/256_100` | 12,700 / 12,800 / 12,900 / 25,600 | 100 complete simultaneous-request windows each |

Use the Inspector to choose a Clip and configure `plan`:

- **Visual**: interactive pause, step, restart and seek. `sideBySidePreview` gives B on the left and A on the right, driven by one clock. Preview owns/disposes its two cameras, restores the original camera, and produces no formal timing report. It may restore state with Seek.
- **FixedSequence**: sampling times are registered during Preparing. Full lifecycle spans the entire Clip; draw-only repeats the checkpoint time. These results are not realtime FPS.
- **Realtime**: the Clip consumes actual unscaled deltas at normal speed, with its own real frame mapping. A slow frame retains consumed-but-never-published events and makes them visible to validation.

For a formal segment, configure `autoRun=true`, `showControls=false` before entering Play Mode. Visible IMGUI controls mark the report non-formal. Only one adapter is active in a timed segment. Pause/focus loss, Inspector configuration changes, explicit scheme/format/control changes, resource loss, or disabling the runner abort the segment. Restart creates a new segment. Changing a source asset does not mutate a renderer's frozen style/layout snapshot.

`DrawOnly` reaches its checkpoint by normal fixed-step playback from zero, including historical publication and expiry. It then freezes; it never calls Seek to compact buckets. `FullLifecycle` warms up separately, resets renderer/input state, records the reset cost, waits through two barrier frames, and starts the complete event interval at zero. Preparation, warmup, barrier and sampling costs are distinguishable.

Controls and raw exports live outside the outer update timer. Whole-frame counters still include the real runner/telemetry overhead; `telemetryMs` is reported separately. Frame arrays are preallocated. Completion only closes the segment; hashing, array trimming and export are deferred until the next Update, after counters for the final sampled frame have been attached.

## Adapters and storage

```csharp
var a = new DamageTextRenderer(geometry: DamageTextGeometry.InstancedQuad);
a.Prepare(configuration, atlas, styles, Shader.Find("UIData/DamageTextInstanced"));

var b = new DamageTextRenderer(positionFormat: DamageTextPositionFormat.Float16);
b.Prepare(configuration, atlas, styles, Shader.Find("UIData/DamageText"));
// Read RequestedPositionFormat, EffectivePositionFormat, FormatReason and Statistics.VertexStride.
```

`DamageTextPlayback` accepts the same constructor options. The direct renderer API is experimental; callers requesting FP16 must preflight their complete input with `DamageTextPrecisionAudit.Evaluate` and use a new preparation/segment for any fallback. The benchmark does that automatically. A supports only Float32 instance properties.

A prepares one shared Quad, identity matrices and front/back instance arrays/MPBs. A group contains at most 128 contiguous characters and matches Shader `forcemaxcount:128`. Platform/Shader/material support is checked before sampling; unsupported A is unavailable rather than silently substituted. Arrays always retain their full capacity, including when an append follows a smaller initial group. Mode, texture or Kind never reorder characters. Local D3D11 tests show 1,024 characters in 8 ordered instanced commands with complete pixels; 129 eight-digit strings use 9 commands. B uses 1 and 2 commands respectively. These are recorded camera command counts, not GPU timings or Frame Debugger captures.

Both adapters share the persistent-bucket lifecycle policy in this experiment. A does not run B's vertex packing or allocate per-bucket meshes. This controls the comparison's scheduler variable; it is not a claim that this is the fastest possible compacting instance pool. All front/back arrays, group capacity, submissions and retained expired characters must remain in reports.

| B vertex field | Float32 offsets | FP16 offsets | Format |
| --- | ---: | ---: | --- |
| local XY | 0 | 0 | Float32×2 / Float16×2 |
| color | 8 | 4 | UNorm8×4 |
| UV | 12 | 8 | Float32×2 |
| world XYZ + spawn | 20 | 16 | Float32×4 |
| motion | 36 | 32 | Float32×4 |
| animation | 52 | 48 | Float32×4 |
| complete stride | **68 B** | **64 B** | queried and validated |

FP16 checks `SupportsVertexAttributeFormat(Float16,2)`, C# packing, actual Mesh formats/dimensions/offsets/stream/stride, and all allocated buckets during Preparing. Unsupported or anomalous layouts rebuild the complete pool as Float32 and retain requested/effective/reason. Format never changes during Sampling. Explicit animation bounds and direct camera submission remain; FP16 does not rely on `RecalculateBounds`.

The complete-Clip precision preflight checks every unique amount/style layout at its maximum visible pop scale. It retains the worst original/quantized local point and pixel error. A candidate above 0.25 px selects Float32 before warmup. This local mathematical bound does not replace graphics, device or art acceptance. The local graphics diagnostic also records quantized/projected points across 2/8 digits, noninteger metrics, maximum 512 px / 4.0 pop amplitude, orthographic/perspective, moving/far anchors and viewport edges. Integer baseline atlas metrics are not substituted for the noninteger test.

For 4,096 vertices, the local-field saving is 16 KiB per mesh and 32 KiB per front/back bucket; indices are unchanged. This implementation retains a Float32 packing scratch plus the half encoding scratch, so CPU array payload can increase even while GPU vertex-buffer capacity falls. Neither the 68→64 B stride nor the 5.9% theoretical reduction is a measured speedup.

## Reports and analysis

The runner exports under `Application.persistentDataPath/DamageTextBenchmark`:

- `*.report.json`: full plan/input/environment metadata, registered times, per-event consumption/result/first publication/end/missed trace, all frame stages and statistics.
- `*.frames.csv`: frame timing and core geometry/refresh/state columns for offline summaries; JSON remains the complete raw record.
- `*.manifest.json`: SHA256 of the exact JSON and CSV. No self-referential report hash is embedded in JSON.

The time table stores readable numeric values plus canonical `registeredTimeBits` IEEE754 Float64 hex strings. Hashing and exact schedule validation use the bit strings: Unity JSON numeric roundtrips can change Double bits. An irregular eight-frame export is checked through the real Unity serializer and the PowerShell analyzer. Reports also contain queried vertex attribute names/formats/dimensions/offsets/streams and stride, not just the requested format.

Source identity uses `damage-comparison-source-v1`: the baseline runtime/HOT/fixed-resource/graphics-settings fingerprint plus comparison assets and Player settings. Reports also retain commit, dirty status, build GUID, package/Unity/backend/device/API, color space, resolution, quality, target rate/vsync, Clip/atlas/style/config/camera/plan/times/request hashes and actual format. The input registry and raw source/artifact manifests bind local verification to actual files.

## One APK for preregistered segments

`Tools/Build-DamageTextComparison.ps1` builds the comparison scene from a clean checkout, embeds all nine Clips, and enables startup capture requests. Use an isolated checkout to preserve local dependency edits:

```powershell
git worktree add --detach Temp/DamageTextAndroidCandidate HEAD
& .\Temp\DamageTextAndroidCandidate\Tools\Build-DamageTextComparison.ps1
```

The build is Release / IL2CPP / ARM64 / Vulkan. It writes source, APK and metadata SHA256s under that checkout's ignored `Builds/DamageTextComparison/` directory, checks ARM64 IL2CPP libraries, and records Vulkan compiler inputs for both shaders, including `INSTANCING_ON`. Successful compilation does not prove device pixels or GPU timing. The wrapper has no install/run action.

The original formal CPU/precision matrix used one clean Release/IL2CPP/ARM64/Vulkan APK. Source fingerprints, APKs, request templates and raw reports stay in local ignored directories. Historical checkpoints are retained locally before squashing; see the [stage archive record](../../openspec/changes/archive/2026-10-28-compare-damage-text-renderers/archive-record.md) for measured results and limitations.

The build temporarily sets Graphics / Instancing Variants to Keep All and restores the original settings and scene afterward. Runtime-created materials are absent from the scene scan, so Unity can otherwise remove required variants; see the [Unity 2022.3 instancing documentation](https://docs.unity3d.com/2022.3/Documentation/Manual/gpu-instancing-shader.html). The build fails if the required Vulkan compiler entries are not observed.

Before launching a device segment, place UTF-8 JSON at `Application.persistentDataPath/DamageTextBenchmark/capture-request.json`. For example:

```json
{
  "schema": "damage-text-capture-request-v1",
  "runId": "static8-draw-A1",
  "clipName": "Static128_8",
  "sourceFingerprint": "COPY_FROM_THE_BUILDS_METADATA_JSON",
  "visualEvidenceSHA256": "unknown",
  "plan": {
    "Mode": 1, "Phase": 0, "Geometry": 1, "PositionFormat": 0,
    "WarmupFrames": 60, "SampleFrames": 3600, "MaximumRealtimeFrames": 36000,
    "Checkpoint": 0.12, "PreparationStep": 0.016666668, "BurstWorkload": false
  }
}
```

`Mode`: Visual=0, FixedSequence=1, Realtime=2. `Phase`: DrawOnly=0, FullLifecycle=1. `Geometry`: B=0, A=1. `PositionFormat`: Float32=0, Float16=1 (B only). Replace the source placeholder with the exact baked fingerprint. The example's unknown visual evidence deliberately leaves ranking unverified; formal acceptance requires the separate matching visual/GPU evidence.

Keep one APK installed for A1/B1/B2/A2; stop the app between segments, replace only the preregistered request, and relaunch. Each report retains `captureRunId` and SHA256 of the UTF-8 request text. Source mismatch, missing/ambiguous embedded Clip, invalid plan, fewer than 3,000 registered formal samples, or a mid-session request change is rejected. The startup file is read once before Preparing and is not polled during Sampling. Missing or invalid startup requests set the runner's `Failure` and do not start a segment. These file-loading/build paths do not validate the deferred device matrix themselves.

Frame statistics are sampled after lifecycle Tick and before camera rendering. Cumulative camera-command/uniform input counters therefore reflect the most recent camera callback; main/render-thread/GC counters are attached on the next Update to the frame that produced them. Those observation points are distinct.

The live-set oracle checks event count, character count and an allocation-free checksum of immutable requests at every sample. Fixed runs use identical registered times. Realtime runs validate the same request interval and final consumption while retaining different real frame boundaries. Event first publication means entry into the drawable set, not proven pixel visibility; near-plane eligibility also does not prove visibility or absence of occlusion.

The offline analyzer independently reconstructs the live set from requests/end times and checks format/group/stride/pass consistency. Coherently changing the reported expected/actual checksum does not bypass that check. Unmeasured event/queue/HashSet memory, preparation GC and post-sample cleanup GC have explicit unavailable scopes.

Metrics distinguish:

- outer event/layout/maintenance/upload CPU, telemetry and full-frame main/render-thread counters, with availability flags; detailed inner-stage timers are marked unavailable rather than inferred;
- logical live/submitted/expired-resident characters, active source buffer capacities, submitted vertices/indices, bucket/pool/front-back object counts, instance groups, stride and one-pass command counts;
- dirty vertex/MPB API input bytes, matrix command inputs and camera-uniform inputs; these are estimates, not measured driver traffic;
- managed geometry-array payload, theoretical vertex/index capacity, Unity-reported owned Mesh/Material object estimates, and unavailable measured GPU memory;
- safe profiler GC counters in Mono, versus explicitly unavailable IL2CPP allocation data. The known unsafe IL2CPP thread-allocation API is not called;
- GPU timing/scope/tool availability. Original formal runs have no validated GPU timing; opt-in native capture reports its instrumented camera interval separately. Missing metrics use flags and `-1`, never a fabricated zero or CPU-derived GPU time.

```powershell
.\Tools\Test-DamageTextBenchmarkAnalyzer.ps1
$reports = @('A1.report.json', 'B1.report.json', 'B2.report.json', 'A2.report.json')
.\Tools\Analyze-DamageTextBenchmark.ps1 -ReportPaths $reports -VisualEvidencePath visual-local.json
```

The analyzer verifies artifact/content hashes, CSV/JSON agreement, request order/identity, no rejection/missed publication/backlog, registered times, state checksums, refresh classification, complete interval, at least 3,000 actual sampled frames, and at least 100 complete windows for burst lifecycle runs. It requires visual evidence bound to source/atlas/style/API, viewport, camera and renderer configuration, and balanced A/B/B/A with matching inputs/environment. Aborted, altered, sparse-insufficient, empty draw-only or padded/nonadvancing lifecycle runs are rejected. Reports preserve every tail sample. Median uses the middle-pair average; P95/P99 use nearest-rank. All-frame and actual-refresh distributions, counts/maxima and per-burst-window summaries are separate.

`refreshFrame` specifically means a geometry/instance rebuild/upload. Command-only removals still contribute to all-frame and burst-window costs. Burst windows extend through the frame that actually observes the last expiry, so cleanup is included even when a frame crosses the nominal end time. Repeated median spreads are reported for outer Update CPU; camera frame-parameter/command-recording work occurs later in camera callbacks and is included only in available whole-frame counters, not this outer timer. Preserve instancing Shader variants when building Player captures, and verify the built variant on the device.

Analyzer fixtures are explicitly synthetic. Likewise the unit test's 3,000 deterministic calls over 25,600 events prove lifecycle accounting, not 3,000 rendered Player frames. They cannot be used as performance evidence. The analyzer currently leaves direct device ranking and GPU/whole-frame conclusions unverified pending GPU frame-binding and complete-frame acceptance.

## Mi 10 conclusions and stage archive

On 2026-10-27 the Mi 10 / Release IL2CPP ARM64 / Vulkan matrix completed 13 ABBA groups, 52 runs and 187,450 sampled frames. Continuous-lifecycle outer Update CPU medians were 0.09035/0.09370 ms for A and 0.17465/0.17055 ms for B32. These timers exclude later camera command recording and do not establish GPU or complete-frame gains. Sparse bursts retain separate all-frame and actual-refresh distributions.

The fractional Vulkan precision fixture passed 32 cases/640 points: 24 cases used actual 64 B FP16 (maximum accepted error 0.11854 px), and 8 cases correctly fell back to 68 B Float32 above the 0.25 px threshold. FP16 reduced theoretical vertex capacity by 5.9%, while continuous-lifecycle outer Update CPU increased by about 0.0327 ms. Art acceptance and complete-cost gains remain unverified; Float32 stays default.

Native Vulkan queries subsequently passed all 1,680 required calibration frames and a separate 3,600-frame non-ranking capture diagnostic. Ten same-APK visual groups/40 images matched for A/B, B32/B16 and instrumentation off/on. The measured scope is `HUD-camera-instrumented`, from BeforeForwardOpaque to AfterEverything, including render-pass store/resume and query overhead; it is not an uninstrumented complete frame. Formal instrumentation-impact ABBA and the 13-group GPU matrix have not run because thermal status remained 3. The original APK's GPU data stays unavailable; Main Thread includes 60 Hz pacing, and Render Thread/safe IL2CPP GC remain unavailable.

Applicable source regression passed 190 EditMode / 61 D3D11 PlayMode tests, 251 governed records and 90.0% UIDataRender line coverage, with separate clean-snapshot validation. On 2026-10-28 the user requested stage archive at 21/22 tasks. GPU task 3.1 stays unchecked and is carried to DAMAGE-03; Jenkins is outside this task, and no RC/tag/release was created. See the [final archive record](../../openspec/changes/archive/2026-10-28-compare-damage-text-renderers/archive-record.md). Raw reports, screenshots, logs, capture archives and build intermediates stay local.

C is excluded for the existing generic holder batch contract: Vector3 + UV/slot/flags + Color32 cannot retain the baseline's per-event Float32 GPU animation/fade. CPU adaptation would include every-frame projection/geometry/color updates plus real holder fill/merge/upload, and would quantize fade before blending. A changed contract/per-event isolation needs its own equivalence gate. This is an integration analysis, not a measured assertion that C is slower. D remains not-integrated until actual ECS systems, Instantiate wrapper, pools, resource ownership, Shader/depth/clipping and real creation/system costs are supplied; no Entities substitute was added.

OpenGLES3, WebGL, URP, business replacement and the separate main-city workload of about 10 global content updates per second retain their own future gates. See the archived [OpenSpec change](../../openspec/changes/archive/2026-10-28-compare-damage-text-renderers/proposal.md) and [DAMAGE-03](../../Documentation~/todo.md) for scope and remaining acceptance work.
