"""Validate the four Android Slim fill reports and summarize nearest-rank percentiles.

Usage: python analyze-slim-fill.py REPORT_DIRECTORY OUTPUT_JSON
The directory contains NN-variant.json, .csv and -metadata.json files.
"""
import csv
import hashlib
import json
import math
import pathlib
import sys

root = pathlib.Path(sys.argv[1])
variants = ["slim-baseline", "slim-fixed", "slim-fixed", "slim-baseline"]
stages = ["Storage", "Vertex", "Uv", "Color", "Index"]
metrics = ["generationMs", "buildMeshMs", "buildMeshDoGenerateMs",
           "buildMeshMeshWriteMs", "mergeMs", "mergePlanMs", "mergeFillMs",
           "meshUploadMs", "frameIntervalMs", "regenerationMs"]
stage_fields = ["buildMeshWrite" + stage + "Ms" for stage in stages]
metrics += stage_fields + ["mainThreadMs", "holderSelectionMs", "buildMeshSetupMs",
    "buildMeshDataInitMs", "buildMeshDispatchMs", "buildMeshSimplePathMs", "buildMeshTransformMs",
    "buildMeshSpriteDataMs", "buildMeshDrawingDimensionsMs"]


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def quantiles(values):
    values = sorted(values)
    result = {name: values[max(0, math.ceil(len(values) * fraction) - 1)]
              for name, fraction in [("median", .5), ("p95", .95)]}
    result.update(min=values[0], max=values[-1])
    return result


runs = []
fingerprints = set()
selection = None
builds = json.loads((root / "android-builds.json").read_text(encoding="utf-8-sig"))
apk_hashes = {item["variant"]: item["sha256"] for item in builds["builds"]}
build_by_variant = {item["variant"]: item for item in builds["builds"]}
for item in builds["builds"]:
    assert item["sourceFingerprint"] == digest(root / item["sourceManifest"])
for index, variant in enumerate(variants, 1):
    path = root / f"{index:02}-{variant}.json"
    report = json.loads(path.read_text(encoding="utf-8-sig"))
    meta = json.loads(path.with_name(path.stem + "-metadata.json").read_text(encoding="utf-8-sig"))
    config = report["configuration"]
    enabled = variant.endswith("-on")
    assert report["schema"] == "hud-rc-v2"
    assert report["scriptingBackend"] == "IL2CPP" and report["executionMode"] == "Player"
    assert report["graphics"] == "Vulkan"
    assert report["quadWriteTimestampFrequency"] > 0
    expected = dict(holderCount=256, quadsPerHolder=3, partialPercent=10,
                    burstEveryFrames=30, warmupFrames=60, sampleFrames=600,
                    seed=12345, useSlim=variant.startswith("slim"),
                    profileQuadWrites=enabled, targetFrameRate=60)
    assert config == expected, (path, config)
    assert meta["reportSha256"] == digest(path)
    assert meta["apkSha256"] == apk_hashes[variant]
    assert meta["sourceFingerprint"] == report["sourceFingerprint"]
    assert report["sourceFingerprint"] == build_by_variant[variant]["sourceFingerprint"]
    fingerprints.add(report["sourceFingerprint"])
    frames = report["frames"]
    assert len(frames) == 1800
    sequence = [(f["mode"], f["sampleFrame"], f["updatedHolders"],
                 f["vertices"], f["indices"], f["batches"], f["selectionChecksum"]) for f in frames]
    if selection is None:
        selection = sequence
    assert sequence == selection, "Workload geometry/selection changed across variants"
    rows = list(csv.DictReader(path.with_suffix(".csv").open(encoding="utf-8-sig", newline="")))
    assert len(rows) == len(frames)
    for f, row in zip(frames, rows):
        available = enabled and f["updatedHolders"] > 0
        writes = f["updatedHolders"] * 3 if enabled else 0
        assert f["buildMeshWriteAvailable"] == available
        assert f["buildMeshWriteSamples"] == writes
        assert row["build_mesh_write_available"].lower() == str(available).lower()
        assert int(row["build_mesh_write_samples"]) == writes
        assert int(row["selection_checksum"]) == f["selectionChecksum"]
        for stage, field in zip(stages, stage_fields):
            number = f[field]
            cell = row[f"build_mesh_write_{stage.lower()}_ms"]
            assert math.isfinite(number)
            if available:
                assert number >= 0 and math.isclose(number, float(cell), rel_tol=1e-12, abs_tol=1e-12)
            else:
                assert number == -1 and cell == "unavailable"
        if not f["scopedAllocationAvailable"]:
            assert all(f[k] == -1 for k in ["generationBytes", "mergeBytes", "measuredThreadBytes"])
        if not f["frameGcAvailable"]:
            assert f["frameGcBytes"] == -1 and row["frame_gc_bytes"] == "unavailable"
        if not f["scopedAllocationAvailable"]:
            assert all(row[k] == "unavailable" for k in ["generation_bytes", "merge_bytes", "measured_thread_bytes"])
        for field in metrics:
            if field != "regenerationMs":
                assert math.isfinite(f[field])
        f["regenerationMs"] = f["generationMs"] + f["mergeMs"]
    groups = {}
    for name, mode, refresh in [("Static", 0, False), ("Partial", 1, False),
                                ("Burst-all", 2, False), ("Burst-refresh", 2, True)]:
        selected = [f for f in frames if f["mode"] == mode and (not refresh or f["updatedHolders"])]
        assert len(selected) == (20 if refresh else 600)
        groups[name] = {"count": len(selected)}
        for metric in metrics:
            values = [f[metric] for f in selected
                      if metric not in stage_fields or f["buildMeshWriteAvailable"]]
            if values:
                groups[name][metric] = quantiles(values)
        valid = [f for f in selected if f["buildMeshWriteAvailable"]]
        groups[name]["writeStageFrames"] = len(valid)
        if valid:
            totals = {stage: sum(f[field] for f in valid) for stage, field in zip(stages, stage_fields)}
            total = sum(totals.values())
            groups[name]["stageSharePercent"] = {stage: value / total * 100 for stage, value in totals.items()}
            groups[name]["substageSumOverAggregatePercent"] = total / sum(f["buildMeshMeshWriteMs"] for f in valid) * 100
    runs.append(dict(order=index, variant=variant, metadata=meta,
                     environment={k: report[k] for k in ["utc", "unityVersion", "os", "cpu", "graphics",
                                  "executionMode", "scriptingBackend", "commit", "sourceStatus",
                                  "sourceFingerprint", "packageVersion", "quadWriteTimestampFrequency"]},
                     configuration=config, groups=groups,
                     availableFrames={k: sum(bool(f[k]) for f in frames) for k in
                                      ["buildMeshWriteAvailable", "mainThreadAvailable",
                                       "frameGcAvailable", "scopedAllocationAvailable"]}))
assert len(fingerprints) == 2 and "unknown" not in fingerprints
result = dict(percentileMethod="nearest rank ceil(n*p)-1, matching HudRcPerformanceRunner",
              runs=runs, validatedFrames=7200, sourceFingerprints=sorted(fingerprints))
result["pairedMedianReductionPercent"] = {
    str(a + 1) + "->" + str(b + 1): {
        group: {metric: (1 - runs[b]["groups"][group][metric]["median"] /
                             runs[a]["groups"][group][metric]["median"]) * 100
                for metric in ["mergeFillMs", "mergeMs", "regenerationMs", "generationMs"]}
        for group in ["Partial", "Burst-refresh"]}
    for a, b in [(0, 1), (3, 2)]}
pathlib.Path(sys.argv[2]).write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
print("Validated 4 reports / 7,200 frames, paired CSVs, identical workload and role-matched source fingerprints.")
for run in runs:
    print(run["order"], run["variant"],
          {name: {k: round(group[k]["median"], 5) for k in
                  ["generationMs", "buildMeshMs", "mergeFillMs", "regenerationMs"]}
           for name, group in run["groups"].items() if name in ["Partial", "Burst-refresh"]})
