# Damage Text Rendering Specification

## Purpose

Define deterministic, allocation-conscious damage text rendering with validated immutable events, ordered persistent mesh buckets, playback-driven GPU animation, atomic publication, and explicit resource ownership.

## Requirements

### Requirement: Immutable validated damage events

系统 SHALL 按值保存独立 EventId、Amount、Kind、WorldPosition、SpawnTime、RandomSeed，支持 10–99,999,999 和 Normal/Critical/Healing。事件接受后内容和布局 SHALL 不变。非法数值/模式、非有限位置/时间、负出生时间、缺字形、重复 ID、迟到或容量不足 SHALL 返回明确结果并分别计数，不得裁剪/覆盖其他事件。去重 SHALL 覆盖同一会话内已结束和待发布事件，拒绝的请求 SHALL 不占用 ID。

#### Scenario: Boundaries and two independent hits
- **WHEN** 同一位置收到 EventId 不同的 10、99、100、99,999,999 及三种模式
- **THEN** 每次均独立接受并保留原始内容，不以同一 holder 的最新状态合并

#### Scenario: Invalid or repeated request
- **WHEN** 请求为 9、100,000,000、非法模式、NaN/Infinity、缺失 glyph，或重用已结束事件的 EventId
- **THEN** 返回对应错误，更新拒绝计数，既有事件及几何保持有效，输入不被静默改写

### Requirement: Ordered bounded event intake

系统 SHALL 配置并预留 pending、驻留事件、桶与会话 ID 容量。due 事件 SHALL 按 `(SpawnTime, EventId)` 排序并追加到逻辑尾部；未来事件 SHALL 保留到出生时刻。输入 SHALL 不得早于最后 Tick 时间或插入已提交排序键之前；截止后且排序有效的请求 SHALL 留到下一帧。主线程 Tick SHALL 每帧形成一个截止点，拒绝重入和倒退时间，同帧重复调用 SHALL 不重复执行。

#### Scenario: Post-cutoff event and future birth
- **WHEN** 本帧 Tick 后提交有效请求，或提前提交未来出生事件
- **THEN** 前者下一帧处理，后者到 due 才处理；同一帧重复 Tick 不再次布局/上传

#### Scenario: Late insertion or exhausted resident slots
- **WHEN** 请求需插入已提交顺序之前，或过期驻留使池容量已满
- **THEN** 分别返回 LateEvent 或 CapacityExceeded，不向旧桶插入、不覆盖存活事件，并保留可审计计数

### Requirement: Cached centered integer layout and frozen resources

系统 SHALL 缓存 0–9 的 UV、尺寸、bearing 和 advance，以复用缓冲执行最多 8 位整数拆分，并以整串布局中心定位。每事件 SHALL 只在出生时布局一次，脏桶重填 SHALL 复制缓存布局，不重复执行 UGUI/TMP 排版或热路径字符串转换。图集、样式、采样状态及版本 SHALL 在一轮播放固定，更改 SHALL 经过显式重启/屏障。

#### Scenario: Eight digits and unequal advances
- **WHEN** 布局 99,999,999，或使用具有不同 advance/bearing 的测试数字
- **THEN** 不截断/溢出，整串中心和间距符合度量；后续桶重填不增加该事件布局次数

#### Scenario: Style changes during playback
- **WHEN** 编辑字号、颜色、寿命或图集
- **THEN** 旧事件不暗中读取新配置，新版本通过重新准备或屏障生效，采样段标记中止

### Requirement: Playback-driven pixel-space GPU animation

系统 SHALL 使用统一 PlaybackTime、事件快照、固定样式、RandomSeed 派生值和指定相机驱动整串移动/缩放/淡出。首期 SHALL 使用 Float32 局部 XY、Float32 世界锚点/时间、Float32 UV 和 Color32，明确并验证全部顶点 stride/offset。字号 SHALL 按目标 viewport 像素控制，缩放围绕整串中心，CPU 稳定播放 SHALL 不改写字符几何。Shader SHALL 不使用私有 _Time 或随机流。

#### Scenario: Pause and changing camera
- **WHEN** PlaybackTime 不变而相机或 RenderTexture 尺寸变化
- **THEN** 事件年龄/动画冻结，投影和像素字号按当前相机/视口正确求值，几何重建/上传计数不增加

#### Scenario: Exact lifetime and near-plane boundary
- **WHEN** age 小于 0、达到 Lifetime，或锚点在相机后方/近裁剪面内
- **THEN** 整串退化为零面积几何，不以仅 alpha 为零留下原片元面积；CPU/GPU 使用同一寿命边界

### Requirement: Persistent ordered mesh buckets

系统 SHALL 使用可配置、默认每桶 128 串的持久桶和有效字符索引范围，仅向逻辑尾桶追加，不向旧桶回填。每截止点每脏桶 SHALL 最多合并/上传一次；无出生/资源变化时 SHALL 不重建/上传，包括时间推进与相机移动。过期字符 SHALL 允许驻留至全桶结束，全桶回收 SHALL 不压缩其他桶；复用物理槽 SHALL 取得新的逻辑尾序。

#### Scenario: Burst crosses a bucket
- **WHEN** 同截止点分别输入 127、128、129 或 256 串，每串最多 8 位
- **THEN** 默认配置下建立正确桶数，每桶最多一次几何提交，只绘制已分配字符；第 128/129 串保持先后顺序

#### Scenario: Stable playback and recycled pool slot
- **WHEN** 旧桶全过期、较晚桶仍存活，随后在回收槽出生新事件
- **THEN** 无出生的存活桶几何保持不变，新事件在逻辑顺序末尾绘制，统计区分 live/submitted/expired-resident 字符与池容量

### Requirement: Atomic publication and explicit recovery

系统 SHALL 复用独立 front/back 几何。本截止点所有脏桶 staging/上传/绑定成功后 SHALL 统一发布；任一失败 SHALL 保留有效 front、accepted pending 和 ID/容量预留，进入失败状态，禁止声称事件已首次发布。资源失效使旧输出不安全时 SHALL 停止相关绘制。恢复 SHALL 显式重建完整状态，资源/配置版本变化 SHALL 标记并单列。

#### Scenario: Failure in the second dirty bucket
- **WHEN** 第一个脏桶 staging 成功而第二个生成、上传或绑定失败
- **THEN** 所有新 front 均不发布，旧输出不半更新，恢复后事件恰好提交一次且不重复派发

#### Scenario: External atlas becomes invalid
- **WHEN** 使用中的 atlas 被销毁或版本失效
- **THEN** 清除不安全绘制命令并报告资源故障，直到显式恢复，不提交可能错误的纹理绑定

### Requirement: Ordered camera submission with current frame parameters

系统 SHALL 绑定单一指定 Built-in 相机，在独占 HUD pass 中按逻辑桶/索引顺序绘制全部 front。默认 SHALL 使用 ZTest Always、ZWrite Off 和普通 Alpha 混合，三模式共享 atlas/材质/单 pass。每帧时间、GPU 相机矩阵和 viewport SHALL 在当前相机绘制前更新，即使 DrawRevision 不变；资源/绘制集合变化 SHALL 失效缓存命令。系统 SHALL 使用显式 Bounds 或不依赖局部字形 Bounds 的直接提交策略，SRP SHALL 明确拒绝。

#### Scenario: Cached draws continue animating
- **WHEN** 多帧无新事件，DrawRevision 不变而时间推进
- **THEN** 缓存命令仍使用本帧 PlaybackTime，移动/缩放/淡出继续，暂停后冻结，几何上传保持不变

#### Scenario: Cross-bucket overlap and far anchor
- **WHEN** 第 128/129 串透明重叠，锚点远离局部字形 Bounds，随后相机移动至视口边缘
- **THEN** 实际图形设备画面符合有序参考，无错误裁剪、倒序或重复提交；null graphics 跳过不能算图形通过

### Requirement: Lifecycle accounting and owned resource cleanup

系统 SHALL 记录 consumed/accepted/rejected、出生/结束/回收、首次发布、漏显与裁剪的不同口径，不能将 CPU 发布当作像素可见证据。全桶结束 SHALL 从绘制集合移除并归还会话池。Clear/停止/Dispose SHALL 幂等地移除并释放自有命令/订阅、Mesh、材质及事件状态，不销毁外部图集/样式/相机。

#### Scenario: Slow frame spans an entire event
- **WHEN** 一次前进跨过某事件完整寿命
- **THEN** 事件仍计入消费和生命周期，无首次发布者计漏显，不静默跳过也不上传已经过期的无用几何

#### Scenario: Clear repeatedly and prepare again
- **WHEN** 连续出生/回收后反复 Clear/停止/Dispose，再重新 Prepare
- **THEN** 无自有资源和订阅泄漏、旧命令残影或旧会话 ID 污染，外部资产仍有效
