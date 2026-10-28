## Why

战斗飘字需要每次伤害独立显示、确定性回放，并在无出生时复用几何；现有 holder 调度是最新状态合并，通用顶点和 Shader 也没有数字动画参数。先建立可信的 Float32 数字负载和生命周期，才能比较合并 Mesh 与字符实例化的完整成本。

依据：需求快照（原始工件仅本地保存）、源码差距审查（原始工件仅本地保存），原代码基线 `0dc902f27fc02631f330d83937f413c792388a52`。M1–M3 已实现：本次 EditMode 185/185、实际图形 PlayMode 39/39、治理 224/224，UIDataRender 行覆盖率 89.4%；见 实施验收（原始工件仅本地保存）。

## What Changes

- 增加 10–99,999,999 的不可变伤害事件、普通/暴击/加血样式、0–9 字形缓存和整串居中的整数布局；输入错误、重复、迟到和容量溢出显式返回并计数。
- 增加统一 PlaybackTime 驱动的相机朝向、像素字号、整串缩放/漂移/淡出 Shader，采用 Float32×2 局部坐标和 Float32 世界锚点。
- 增加持久数字桶：默认每桶 128 串，出生仅布局一次，每帧截止点每脏桶最多上传一次；稳定动画与相机运动保持几何不变，过期几何退化，全桶结束后回收。
- 使用独立 front/back staging、有序逻辑桶与 Built-in 单相机提交，明确失败恢复、每帧时间更新和自有资源释放。
- 在现有 Sample 增加固定事件 Clip、版本/哈希/相机轨迹、播放/暂停/重启/跳转/单步/速度/Clip 与方案选择，以及独立的基础统计和采样段失效规则；首期仅启用 Mesh 方案。
- 增加包 EditMode 与 Sample 图形 PlayMode 验证和需求追溯，不将已有 holder 证据当作数字负载已通过。

## Capabilities

### New Capabilities

- `damage-text-rendering`: 数字事件验证、字形布局、GPU 动画、持久有序桶、容量/生命周期、原子发布及资源所有权。
- `damage-text-playback`: 不可变 Clip、统一时钟、确定性派发/恢复、交互控制和基础诊断状态。

### Modified Capabilities

无。复用 `IHolderDrawSource` 和 Built-in 顺序提交，保留现有 holder、主城调度、包分发与发布规格。

## Impact

增加了 `com.uidatarender/Runtime/UIDataRender/DamageText/`、专用 Shader、`com.uidatarender/Tests/EditMode` 测试，以及 `HUDRenderTestSample` 的驱动、图集/样式/Clip/场景和图形测试；沿用 UIDataRender、HOT、Tests、Tests.PlayMode 程序集边界及 `.meta`/治理约束。新增行为显式接入，不增加 Entities 或新 Unity 工程。

后继 [compare-damage-text-renderers](../2026-10-28-compare-damage-text-renderers/proposal.md) 承接 M4–M5 的实例化、GPU/完整生命周期和 FP16 实验。真实 ECS、URP14、主城负载与产品峰值属于后续 M6；1.5 ms 仅为同负载建基线后的探索参考。
