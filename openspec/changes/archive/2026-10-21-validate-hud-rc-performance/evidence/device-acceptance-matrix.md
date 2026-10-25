# PERF-04 真机验收记录

当前状态（2026-10-20）：`inconclusive/environment-limited`。后续 Release IL2CPP 测量已具有 APK/source/config/report 哈希和重复采样证据，但 scoped/frame GC 计数器仍不可用，尚无精确 RC 候选验收。最新结果、64/256 × Managed/Slim 矩阵缺口及 M5 交接见 [收尾材料](closure-handoff-2026-10-20.md)。下方设备表为 2026-09-27 历史快照。

## 验收规则

当前任务只验收一台设备：**Xiaomi 10**。缺少任一必填字段、计数器校准失败、采样被后台/温控/日志写入污染，状态为 `inconclusive/environment-limited`，不进入性能门判断。

环境有效后，使用同一候选提交、同一场景配置和相同预热/采样参数运行 Static、Partial、Burst 三种模式，并保存 JSON、CSV、日志和 source manifest。

## 设备记录

| 设备 | 系统版本 | Player 构建/提交 | Graphics API | 刷新率 | 电源/温度 | 环境门 | 性能门 | 原始证据 |
|---|---|---|---|---:|---|---|---|---|
| Xiaomi 10 | Android 11 (`RKQ1.200826.002`) | ARM64 + IL2CPP + non-Development r3；package 0.1.2；APK SHA256 `D9F21CFD13849FF3A9E0870F9B92F4301BFCEA71276FABB4D20187401899AB53` | Vulkan；Player 存活并导出报告 | 60 Hz | USB，100%，32.7°C | `inconclusive/traceability-incomplete` | `not-run` | [r3 report](xiaomi10/r3-report/hud_rc_20260927_094916_659_bdbf6896.json), [CSV](xiaomi10/r3-report/hud_rc_20260927_094916_659_bdbf6896.csv) |

设备连接快照（2026-09-27）：ADB serial `791d02af`，model `Mi 10`，device `umi`，分辨率 `1080x2340`，density `440`，SurfaceFlinger 报告刷新率约 `60.000002 fps`。该快照只证明设备可连接，不代表 Player 已安装或性能测试已执行。

## 每台设备必填字段

- 精确型号、Android/系统版本、设备温度与电源状态。
- Unity Player 构建标识、候选提交 SHA、package version、场景/配置版本。
- Graphics API、分辨率、刷新率、vSync、targetFrameRate、质量设置。
- holder 数量（64、256）、路径（UIMeshData、UIMeshDataX）、模式、更新比例和频率。
- 预热帧数、采样帧数、重复轮数、seed，以及是否发生后台切换或降频。
- regeneration CPU、主线程/帧时间 median/p95、集中刷新帧摘要、分配计数器状态。
- JSON/CSV/日志/source manifest 路径和 SHA256；失败或中断也要保留原始日志。

Android Player 构建和安装入口已补充：`Tools/Invoke-HudRcAndroidValidation.ps1 -Action Build` 默认生成 ARM64 + IL2CPP + 非 Development APK，`-Development` 仅用于接线调试，`-Action Install` 安装到指定 ADB 设备。r3 已于 2026-09-27 安装并稳定运行，导出 540 帧报告；此前 r1/r2 崩溃及修复摘要见 [crash log](xiaomi10/il2cpp-release-crash.txt)。报告确认 Vulkan/IL2CPP/Player，但 `commit/sourceStatus/sourceFingerprint=unknown`，且 `scopedAllocationAvailable=false`，所以不满足完整环境有效性门。

## 状态转换

- `not-run` → 设备已登记但没有运行结果。
- `inconclusive/environment-limited` → 结果存在但环境门未通过。
- `valid-measurement` → 环境门通过，允许判断性能结果。
- `passed` → 环境有效且满足选定门槛。
- `failed` → 环境有效且超过门槛；此时才可提交预算例外或重开优化。

真机结果完成后，将本表状态和原始证据链接回 `hud-scene-results.md`，再更新 `PERF-04` 和 M5 的 RC 交接记录。Mate 40 Pro 不属于本任务范围。
