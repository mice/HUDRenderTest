## ADDED Requirements

### Requirement: Dedicated Sample performance scene
仓库 SHALL 提供提交到 HUDRenderTestSample/Assets/Scenes/Scene_Perf_HUD_RC.unity 的专用验收场景及所需 .meta，通过实际包更新和渲染路径执行负载。场景与测量 driver MUST 属于 Sample，不能作为包运行时依赖。

#### Scenario: Scene opens from the repository
- **WHEN** 从干净检出打开专用场景并运行默认配置
- **THEN** 无缺失脚本/引用，实际创建 HUD 对象并执行非零网格更新，输出可核对的工作量

### Requirement: Deterministic workload modes
场景 SHALL 支持静态基线、持续局部更新、集中刷新，记录可重放的数量、复杂度、比例、频率和 seed。集中刷新帧 SHALL 单独汇总，避免被静态帧稀释。

#### Scenario: Same configuration is replayed
- **WHEN** 相同配置和 seed 再次运行
- **THEN** 更新对象顺序及计划更新数量一致，实测耗时可变化但负载定义不变

### Requirement: Uncontaminated measurement and repeatable reset
场景 SHALL 分离准备、预热、采样和导出，采样区间 MUST 排除测量工具自身的日志及文件写入，保存原始逐帧数据和摘要。缺失计数器 MUST 标记 unavailable。Reset SHALL 清理所建对象并恢复修改的全局设置。

#### Scenario: Unsupported profiling metric
- **WHEN** 当前运行环境无法提供某个主线程或分配指标
- **THEN** 报告明确标记 unavailable 和影响，不用 0 冒充通过，受影响的验收保持待补

#### Scenario: Session is repeated
- **WHEN** 一轮采样导出后执行 Reset 并再次运行
- **THEN** 没有上一轮残留对象或统计，timeScale/vSync/targetFrameRate 等修改可恢复，导出数据与各自配置一致

### Requirement: Reproducible representative scene
首个 v0.1.2 RC 发布前 SHALL 在 Unity 中验证代表性 HUD 场景，记录设备、软件环境、构建模式、负载、更新频率、预热和采样，并报告 regeneration CPU、主线程/帧时间 median/p95 和分配。

#### Scenario: Microbenchmark exists without scene data
- **WHEN** 归档 PERF-01 只有四顶点/六索引微基准
- **THEN** 补充可重复场景证据，不将微基准直接当成场景验收

### Requirement: Measurement attribution and comparison
证据 SHALL 区分 extraction 与场景整体分配，有优化时保持前后配置一致，无修改时 MUST NOT 宣称性能改善。

#### Scenario: Unchanged production implementation
- **WHEN** 当前路径没有运行时优化
- **THEN** 报告标为当前实现验证，明确环境和适用范围

#### Scenario: Other scene systems allocate
- **WHEN** 场景存在分配但 warmed extraction 为 0 B
- **THEN** 分别记录归因，不声称整个应用零 GC

### Requirement: Environment validity gate
性能结果 SHALL 先通过环境有效性检查，再用于 RC 性能结论。检查至少包括运行载体（Player 或明确标注的 Editor）、Unity/backend、计数器有效性、重复运行稳定性和采样是否受编辑器调度或工具写入污染。环境不满足要求时，结果 MUST 标记为 `inconclusive/environment-limited`，不得直接判定产品性能失败，也不得用预算例外替代环境缺口。

#### Scenario: Editor-only or unavailable counter
- **WHEN** 结果只来自 Editor/Mono，或关键分配计数器校准失败，或重复性不足
- **THEN** 保留原始数据和适用范围，将 RC 性能状态标记为 `inconclusive/environment-limited`；建立 Player/目标设备复测任务后，才重新判断性能门槛。

#### Scenario: Valid environment with measured regression
- **WHEN** Player/目标设备和计数器均有效，重复采样满足配置要求，且结果超过已选性能门槛
- **THEN** 才能标记为 `failed` 或提交明确的预算例外；例外必须记录预算、风险、重开条件和用户确认。

### Requirement: Confirmed performance budget exception
在环境有效性门通过后，未达 5 us / 30x 比较参考时 SHALL 提供场景实测、使用预算、残余风险和重开条件，并取得用户明确例外确认后才能通过发布性能门槛。环境无效时不得要求用户为环境缺口确认例外。

#### Scenario: Historical report already says accepted
- **WHEN** 旧报告接受预算但没有本轮要求的场景证据和用户确认
- **THEN** 新后续任务保持 pending，不以旧措辞替代验收

#### Scenario: Supported budget is confirmed
- **WHEN** 测量支持预算且用户明确确认例外
- **THEN** 记录确认与适用限制，允许向 RC 候选验证交接

### Requirement: Traceable follow-up and bounded scope
新场景验收 SHALL 使用稳定后续 ID 链接归档 PERF-01，M3 其他性能项只有满足各自验收才关闭。候选-wide 验证 SHALL 交由 M5，性能任务完成 MUST NOT 自动发布 tag。

#### Scenario: Original performance item is archived
- **WHEN** 新增 RC 场景要求
- **THEN** 保留原归档、创建后续 ID，在 Documentation~ 记录结果，不复用旧 ID

#### Scenario: GPU and Job code is unchanged
- **WHEN** 本次未触及 GPU 槽位或 Job 路径
- **THEN** 完整对应矩阵仍留后续；若触及这些路径则新增相应验证
