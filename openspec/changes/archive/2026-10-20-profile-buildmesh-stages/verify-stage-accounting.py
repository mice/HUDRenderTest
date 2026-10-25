"""Validate stage containment in the preserved quad comparison ZIP.

Usage: python verify-stage-accounting.py PATH_TO_ZIP OUTPUT_JSON
Run from repository root to also verify the sampled Runtime/runner source hashes.
"""
import hashlib
import json
import math
import pathlib
import sys
import zipfile

archive = pathlib.Path(sys.argv[1])
fields = ["Setup", "DataInit", "Dispatch", "DoGenerate", "SimplePath",
          "SpriteData", "DrawingDimensions", "MeshWrite", "UguiFallback", "Transform"]
frame_count = 0
refresh_count = 0
with zipfile.ZipFile(archive) as evidence:
    names = [n for n in evidence.namelist()
             if n.endswith(".json") and not n.endswith("-metadata.json") and n[:2].isdigit()]
    assert len(names) == 8
    for name in names:
        report = json.loads(evidence.read(name))
        for f in report["frames"]:
            frame_count += 1
            refresh_count += int(f["updatedHolders"] > 0)
            for field in fields:
                value = f["buildMesh" + field + "Ms"]
                assert math.isfinite(value) and value >= -1e-9, (name, field, value)
            total = sum(f["buildMesh" + field + "Ms"] for field in ["Setup", "DataInit", "Dispatch", "DoGenerate"])
            assert total <= f["buildMeshMs"] + 1e-9
            assert f["buildMeshMs"] <= f["generationMs"] + 1e-9
            assert f["buildMeshSimplePathMs"] + f["buildMeshUguiFallbackMs"] <= f["buildMeshDoGenerateMs"] + 1e-9
            assert f["buildMeshSpriteDataMs"] <= f["buildMeshDrawingDimensionsMs"] + 1e-9
            assert f["buildMeshDrawingDimensionsMs"] + f["buildMeshMeshWriteMs"] <= f["buildMeshSimplePathMs"] + 1e-9
            assert f["buildMeshTransformMs"] <= f["buildMeshSimplePathMs"] + f["buildMeshUguiFallbackMs"] + 1e-9
    checked_sources = []
    for line in evidence.read("android-sources.sha256").decode("utf-8-sig").splitlines():
        sha, name = line.split("  ", 1)
        if name.startswith("com.uidatarender/Runtime/") or name == "HUDRenderTestSample/Assets/Scripts/HOT/HudRcPerformanceRunner.cs":
            assert hashlib.sha256(pathlib.Path(name).read_bytes()).hexdigest().upper() == sha, name
            checked_sources.append(name)
result = dict(reports=len(names), frames=frame_count, refreshFrames=refresh_count,
              accounting="passed", runtimeSourceHashChecks=len(checked_sources),
              archiveSha256=hashlib.sha256(archive.read_bytes()).hexdigest().upper(),
              limitations="Nested stage containment only; no speed thresholds or release/zero-GC acceptance.")
pathlib.Path(sys.argv[2]).write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
print(json.dumps(result))
