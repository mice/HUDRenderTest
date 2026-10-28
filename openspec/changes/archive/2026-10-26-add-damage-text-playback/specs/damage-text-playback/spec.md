## ADDED Requirements

### Requirement: Immutable versioned playback clip

播放器 SHALL 预先生成或加载不可变 Clip，记录格式/生成算法版本、内容哈希、全部事件字段、样式/图集/配置版本与相机轨迹。Preparing SHALL 验证唯一 EventId、有限时间/位置、数值/模式/字形及配置容量，按 `(SpawnTime, EventId)` 确立顺序；渲染器 SHALL 不再次生成随机事件。首版 SHALL 显式校验有限相对时间范围，默认最长 1,800 秒，超限不得静默重置原点。

#### Scenario: Same clip in repeated sessions
- **WHEN** 多次加载同一 Clip 和配置
- **THEN** 内容哈希、排序及派发字段一致，渲染器 RandomSeed 只影响约定效果，不改变事件集合

#### Scenario: Invalid clip is prepared
- **WHEN** Clip 包含重复 ID、越界金额、非有限时间或超出配置时长
- **THEN** Preparing 明确失败并指出输入，不进入正式采样或改写数据

### Requirement: Single monotonic clock and lossless interval dispatch

播放器 SHALL 提供单一 PlaybackTime，向前运行单调，CPU、Shader、相机轨迹和各方案 SHALL 使用相同采样时刻。正常派发 SHALL 覆盖 `(previousTime, currentTime]`，初始化 SHALL 单独处理起点事件，每事件恰好消费一次。慢帧内完整出生并结束的事件 SHALL 仍进入生命周期/漏显统计。倒退 SHALL 由恢复处理，不能直接 Tick 倒退时间。

#### Scenario: Start-time and slow-frame events
- **WHEN** Clip 在 0 秒含事件，后续一个慢帧跨过多个出生和结束
- **THEN** 起点不漏派也不重派，跨度内所有事件均消费，未显示者有明确统计

#### Scenario: Pause and fixed steps
- **WHEN** 暂停多帧后以固定步长单步
- **THEN** 暂停期间时间/存活状态/动画不变且继续绘制，单步与相同目标时刻的固定序列一致

### Requirement: Deterministic restart and seek

播放器 SHALL 提供重启和指定时刻跳转，使用 Clear + Prepare/重建目标时刻存活状态或等价快照恢复。恢复后 SHALL 与正常回放到同一时刻具有相同存活 EventId、金额、类型、世界锚点、动画和排序。恢复耗时 SHALL 单列，恢复后的桶历史 SHALL 不冒充正常运行负载；固定画面性能检查点 SHALL 正常回放到达后冻结。

#### Scenario: Seek backwards and replay forwards
- **WHEN** 先跳到较晚时刻，再向后跳转并前进
- **THEN** 不向生命周期 Tick 倒退时间，目标状态与参考一致，后续事件不重复/遗漏

#### Scenario: Seek to exact expiry
- **WHEN** 跳转目标等于某事件 SpawnTime + Lifetime
- **THEN** 该事件不在存活集合，边界与普通回放和 Shader 一致

### Requirement: Playback controls and explicit states

Sample SHALL 提供播放、暂停、重新开始、时间轴拖动、指定时间、固定步长单步、速度、Clip 和渲染方案选择，并显示可用方案。状态 SHALL 包含 Idle、Preparing、Warming、Sampling、Paused、Completed、Failed。预览 SHALL 允许暂停；正式采样中的暂停/单步/跳转/改速度/Clip/方案/配置 SHALL 终止并标记当前段，不能继续拼接。

#### Scenario: Preview controls operate
- **WHEN** 使用暂停、单步、重启、拖动和速度控制
- **THEN** 状态与时钟一致，当前可用 Mesh 方案正常显示，未接入方案明确不可选择

#### Scenario: Formal sample is interrupted
- **WHEN** Sampling 时用户跳转或改变任何采样配置
- **THEN** 导出/标记 aborted 及原因，进入预览或新段，已中止段不计作完整性能运行

### Requirement: Separated diagnostics and reproducible metadata

播放器 SHALL 在独立、可关闭/降频的面板显示时间、存活串/字符、提交/过期驻留字符、桶/Draw、出生/结束/回收、接受/拒绝/漏显，并使显示刷新与测量逻辑分离。每段 SHALL 记录代码提交/源码状态及指纹、Clip/样式/图集/配置哈希、构建、设备、Unity、分辨率、相机和图形 API；未知数据 SHALL 显式标记，不能填零冒充结果。

#### Scenario: Statistics display is disabled
- **WHEN** 关闭面板进行重复回放
- **THEN** 事件、动画、绘制和记录内容不变，UI 刷新不进入被比较的事件处理范围

#### Scenario: Source or metric is unavailable
- **WHEN** 构建无法取得代码指纹或某项设备指标
- **THEN** 报告保留 unknown/unavailable 及有效性状态，不被标记为完整可追溯验收
