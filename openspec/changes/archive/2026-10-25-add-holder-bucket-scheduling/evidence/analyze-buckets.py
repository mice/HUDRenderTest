"""Validate matched bucket captures, CSV parity, request completion and two-frame costs.

Usage: python analyze-buckets.py REPORT_DIRECTORY OUTPUT_JSON [PREFIX_COUNT]
Requires six balanced captures, a seventh legacy control, and android-builds.json.
"""
import csv
import hashlib
import json
import math
import pathlib
import sys

root = pathlib.Path(sys.argv[1])
variants = ['bucket-single', 'bucket-same', 'bucket-rotating', 'bucket-rotating', 'bucket-same', 'bucket-single', 'bucket-legacy']
strategies = {'bucket-single': 2, 'bucket-same': 1, 'bucket-rotating': 0}
limit = int(sys.argv[3]) if len(sys.argv) > 3 else len(variants)
assert 1 <= limit <= len(variants)


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def stats(values):
    ordered = sorted(values)
    assert ordered
    return {'count': len(ordered), 'median': ordered[math.ceil(len(ordered) * .5) - 1],
            'p95': ordered[math.ceil(len(ordered) * .95) - 1], 'max': ordered[-1]}


def checksum(indices):
    value = 0
    for index in indices:
        value = ((value * 31 + index + 1) + 2**31) % 2**32 - 2**31
    return value


builds = read(root / 'android-builds.json')['builds']
build_by_variant = {b['variant']: b for b in builds}
fingerprints = {b['sourceFingerprint'] for b in builds}
assert len(fingerprints) == 1, 'All APKs must use the same source manifest.'
for b in builds:
    assert digest(root / b['sourceManifest']) == b['sourceFingerprint']
reference_requests = reference_outputs = reference_final = None
runs = []
for order, variant in enumerate(variants[:limit], 1):
    stem = f'{order:02}-{variant}'
    path = root / (stem + '.json')
    report = read(path)
    meta = read(root / (stem + '-metadata.json'))
    config = report['configuration']
    bucket = variant != 'bucket-legacy'
    expected = dict(holderCount=256, quadsPerHolder=3, partialPercent=10, burstEveryFrames=30,
                    warmupFrames=60, sampleFrames=3000, seed=12345, useSlim=True, profileQuadWrites=False,
                    disableBuildMeshDiagnostics=True, useGenerationBatch=True, useBucketScheduling=bucket,
                    bucketStrategy=strategies.get(variant, 0), spriteBacked=False, forceUguiFallback=False, targetFrameRate=60)
    assert config == expected, (variant, config)
    assert report['schema'] == ('hud-rc-buckets-v1' if bucket else 'hud-rc-v2')
    assert report['scriptingBackend'] == 'IL2CPP' and report['executionMode'] == 'Player'
    assert report['graphics'] == 'Vulkan'
    assert report['preparedSpriteImages'] == 0 and report['preparedFastPathImages'] == 3
    assert report['sourceFingerprint'] in fingerprints
    assert meta['sourceFingerprint'] == report['sourceFingerprint']
    assert meta['reportSha256'] == digest(path)
    assert meta['apkSha256'] == build_by_variant[variant]['sha256']
    frames = report['frames']
    assert len(frames) == (9006 if bucket else 9000)
    with (root / (stem + '.csv')).open(encoding='utf-8-sig', newline='') as f:
        csv_frames = list(csv.DictReader(f))
    assert len(csv_frames) == len(frames)
    if bucket:
        offsets = {s['unityFrame'] - (s['mode'] * 3062 + 61 + s['sampleFrame']) for s in frames}
        assert len(offsets) == 1, 'Runner frame indices must map directly to actual Unity frames.'
    expected_requests = []
    versions = [0] * 256
    final_versions = []
    by_mode = {}
    for mode in range(3):
        versions = [v + 60 for v in versions]  # Every warmup frame submits all holders.
        group = [s for s in frames if s['mode'] == mode]
        assert len(group) == (3002 if bucket else 3000)
        by_mode[mode] = group
        for frame, sample in enumerate(group):
            assert sample['sampleFrame'] == frame
            drain = frame >= 3000
            count = 0 if drain or mode == 0 else 26 if mode == 1 else 256 if frame % 30 == 0 else 0
            start = (frame * max(1, count) + 12345) % 256
            selected = [(start + i) % 256 for i in range(count)]
            assert sample['selectionChecksum'] == checksum(selected)
            assert sample['vertices'] == 3072 and sample['indices'] == 4608
            assert not sample['buildMeshStagesAvailable'] and not sample['buildMeshWriteAvailable']
            assert sample['buildMeshMs'] == -1 and sample['buildMeshDoGenerateMs'] == -1
            assert sample['buildMeshWriteSamples'] == 0
            if bucket:
                assert sample['bucketScheduling'] and sample['drainFrame'] == drain and not sample['barrierFrame']
                assert sample['requestedHolders'] == count
                assert sample['updatedHolders'] == sample['completedHolders']
                buckets = 1 if variant == 'bucket-single' else 2
                assert sample['logicalBuckets'] == buckets and sample['batches'] == buckets
                assert sample['refreshedBuckets'] <= (1 if variant == 'bucket-rotating' else buckets)
                assert sample['generationBatchCount'] == sample['refreshedBuckets'] * 3
                assert 0 <= sample['completedFirstPendingAge'] <= 1
                assert 0 <= sample['firstPendingAge'] <= 1
                assert sample['requestedVersionSum'] >= sample['completedVersionSum']
                assert math.isclose(sample['updateCpuMs'], sample['generationMs'] + sample['mergeMs'], abs_tol=1e-8)
                assert sample['hudCpuMs'] + 1e-8 >= sample['updateCpuMs'] + sample['drawSubmitMs'] + sample['telemetryMs']
                for holder in selected:
                    versions[holder] += 1
                    request_frame = mode * 3062 + 61 + frame
                    expected_requests.append((holder, mode, frame, request_frame, versions[holder]))
            else:
                assert sample['updatedHolders'] == count and sample['batches'] == 1
                assert sample['generationBatchCount'] == (3 if count else 0)
            if not sample['scopedAllocationAvailable']:
                assert sample['measuredThreadBytes'] == -1
        if bucket:
            assert group[-1]['pendingHolders'] == 0
            final_versions.extend((i, mode, version, version) for i, version in enumerate(versions))
    for sample, row in zip(frames, csv_frames):
        assert int(row['updated_holders']) == sample['updatedHolders']
        assert int(row['selection_checksum']) == sample['selectionChecksum']
        assert row['build_mesh_setup_ms'] == 'unavailable'
        assert math.isclose(float(row['generation_ms']), sample['generationMs'], abs_tol=1e-10)
        assert math.isclose(float(row['merge_ms']), sample['mergeMs'], abs_tol=1e-10)
    if bucket:
        requests = report['bucketRequests']
        actual_requests = [(r['holder'], r['mode'], r['sampleFrame'], r['requestFrame'], r['requestVersion']) for r in requests]
        assert actual_requests == expected_requests
        for request in requests:
            assert 0 <= request['completedFrame'] - request['requestFrame'] <= 1
            assert request['completedVersion'] >= request['requestVersion']
        final = [(s['holder'], s['mode'], s['requestedVersion'], s['completedVersion']) for s in report['bucketFinalStates']]
        assert final == final_versions
        if reference_requests is None:
            reference_requests, reference_outputs, reference_final = actual_requests, report['bucketOutputChecksums'], final
        assert actual_requests == reference_requests and final == reference_final
        assert report['bucketOutputChecksums'] == reference_outputs
        with (root / (stem + '-buckets.csv')).open(encoding='utf-8-sig', newline='') as f:
            rows = list(csv.DictReader(f))
        assert len(rows) == len(frames)
        mapping = {'requested': 'requestedHolders', 'completed': 'completedHolders', 'pending': 'pendingHolders',
                   'logical_buckets': 'logicalBuckets', 'refreshed_buckets': 'refreshedBuckets', 'meshes': 'batches',
                   'completed_first_pending_age': 'completedFirstPendingAge', 'request_checksum': 'selectionChecksum',
                   'requested_version_sum': 'requestedVersionSum', 'completed_version_sum': 'completedVersionSum'}
        for row, sample in zip(rows, frames):
            for field, attr in mapping.items(): assert int(row[field]) == sample[attr], (stem, field)
            assert (row['drain'].lower() == 'true') == sample['drainFrame']
            assert math.isclose(float(row['update_cpu_ms']), sample['updateCpuMs'], abs_tol=1e-10)
        assert report['bucketOwnedRenderers'] == (2 if variant == 'bucket-single' else 4)
        assert report['bucketOwnedMeshes'] == report['bucketOwnedRenderers'] == report['bucketOwnedMaterials']
    ordinary = [s for s in frames if not s['drainFrame']]
    refresh = [s for s in ordinary if s['mode'] == 2 and s['updatedHolders'] > 0]
    assert len(refresh) == (200 if variant == 'bucket-rotating' else 100)
    update = lambda s: s['updateCpuMs'] if bucket else s['generationMs'] + s['mergeMs']
    windows = [by_mode[2][f:f+2] for f in range(0, 3000, 30)]
    run = dict(order=order, variant=variant, frames=len(frames), burstRefresh=stats([update(s) for s in refresh]),
               burstGeneration=stats([s['generationMs'] for s in refresh]), burstMerge=stats([s['mergeMs'] for s in refresh]),
               burstDrawSubmission=stats([s['drawSubmitMs'] for s in refresh]),
               twoFrameUpdate=stats([sum(update(s) for s in window) for window in windows]),
               ordinaryRefresh=stats([update(s) for s in ordinary if s['updatedHolders'] > 0]),
               partialUpdate=stats([update(s) for s in ordinary if s['mode'] == 1]),
               over1_5ms=sum(update(s) > 1.5 for s in refresh),
               allocationCounterAvailable=any(s['scopedAllocationAvailable'] for s in frames),
               batteryBefore=meta['batteryBefore'], batteryAfter=meta['batteryAfter'],
               thermalBefore=meta['thermalBefore'], thermalAfter=meta['thermalAfter'])
    if bucket:
        run.update(burstHudCpu=stats([s['hudCpuMs'] for s in refresh]),
                   twoFrameHudCpu=stats([sum(s['hudCpuMs'] for s in w) for w in windows]),
                   maxRequestDelay=max(r['completedFrame']-r['requestFrame'] for r in requests),
                   requestCount=len(requests), outputChecksums=report['bucketOutputChecksums'],
                   initializationMs=report['bucketInitializationMs'], ownedMeshes=report['bucketOwnedMeshes'],
                   ownedUnityObjectBytes=report['bucketOwnedUnityObjectBytes'],
                   ownedUnityObjectBytesAvailable=report['bucketOwnedUnityObjectBytesAvailable'])
    runs.append(run)
result = dict(sourceFingerprint=next(iter(fingerprints)), validation='passed' if limit == len(variants) else 'partial-prefix', runs=runs,
              limitations='Local 1.5 ms p95 target only. RC/GC acceptance remains open. Owned Unity object bytes exclude managed scratch/queue buffers and external assets.')
pathlib.Path(sys.argv[2]).write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
for run in runs:
    print(run['variant'], 'burst p95', run['burstRefresh']['p95'], 'two-frame p95', run['twoFrameUpdate']['p95'])
