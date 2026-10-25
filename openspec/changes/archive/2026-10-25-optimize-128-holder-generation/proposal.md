## Why

Mi 10 上 128 个 holder 同步全量更新的两轮 p95 为 3.7467 / 3.7664 ms，仍超过用户提出的 1.5 ms 目标。生成阶段中位数约 3.32 ms、合并约 0.32 ms，因此先优化生成，再推进已规划的两帧分桶。

## What Changes

- 固定 128-holder 同步整体更新的性能基线，以 generation + merge（含上传）p95 ≤ 1.5 ms 为工作验收目标。
- 优先量化和消除重复 Unity 对象查询、准备及转换成本，保持当前几何、纹理和同步可见性语义。
- 根据整体预算决定是否需要 Simple quad 纯数据批量生成和 C#/Burst/Job 对照；若查询复用已满足目标，将 Burst/SIMD 原型及 ARM64 检查明确移交 JOB-01。
- 保留通用路径及回退，补充等价性与分配测试，记录可复现的同配置真机对照。
- 分桶策略继续由 `add-holder-bucket-scheduling` 跟踪，实施优先级排在本项之后。

## Capabilities

### New Capabilities

- `hud-128-generation-performance`: 128-holder 同步刷新预算、优化路径等价性、端到端成本和实测证据要求。

### Modified Capabilities

无。现有 RC 预算和未通过状态不因本次局部优化目标而变更。

## Impact

预计涉及 `UIImage`、`DataPrefabHolder`、mesh data、批量生成入口及 Sample 性能驱动；Burst/Collections 使用仓库现有依赖。具体实现由热点实验决定，不预先承诺重写公共 API 或将托管 Unity 对象放入 Job。

基线与来源见 [128-holder 实测](../../add-holder-bucket-scheduling/evidence/holders128-results.md)。2026-10-25 已实现可关闭的诊断计时和显式批量查询复用候选，通过本地回归；四轮真机对照已通过，查询复用的两轮 p95 为 1.2971 / 1.2310 ms。尚未实现 Burst 生成；它已后置到 JOB-01，诊断开销下降与查询复用收益分别记录。
