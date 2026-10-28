## Context

实施状态（2026-10-26）：本 change 的 M1–M3 已完成，验证与残余范围见 实施验收（原始工件仅本地保存）。下述设计决策已由 Runtime/Sample 和新增 32 项测试落实；精确顶点 offset、RenderTexture Y 翻转、near-plane eligibility 与报告边界见使用契约。

本设计落实[提案](proposal.md)的 M1–M3，输入为需求快照（原始工件仅本地保存）和差距审查（原始工件仅本地保存）。基线 `0dc902f27fc02631f330d83937f413c792388a52` 已具备 holder 缓存、front/back、连续分批与 Built-in 有序提交；数字事件、专用动画顶点和播放器尚不存在。

包负责可复用的事件、字形、布局、生命周期和绘制；Sample 负责 Clip 生成/选择、交互、相机轨迹和采样编排。沿用 UIDataRender/HOT 和既有测试程序集。用户已授权完全控制 Unity 编辑器，实施时优先 MCP，不新建验证工程。

## Goals / Non-Goals

**Goals:** 三模式 2–8 位正确显示；固定输入与时钟可重现；内容只在出生时布局；同帧出生合并；无出生时动画和相机移动不写几何；顺序、失败、容量和资源边界可测。

**Non-Goals:** M4–M5 的实例化/性能排名/FP16；真实业务 ECS、目标跟随、低于 10 的数值、前导零/加号、URP/多相机和正式性能预算。主城继续使用已有状态调度器。

## Decisions

### 1. 不可变事件、会话与有界队列

建议类型为 `DamageTextRequest`、`DamageTextKind`、`DamageTextSpawnResult`、`DamageTextRenderer`。请求字段沿用需求：uint EventId/Amount/RandomSeed、Kind、WorldPosition、float SpawnTime。构造后按值复制；样式、字形与配置在 Preparing 时冻结版本和内容哈希。

`Spawn` 只验证和预留，返回 Accepted、InvalidAmount、InvalidKind、InvalidPosition、InvalidTime、MissingGlyph、DuplicateEventId、LateEvent、CapacityExceeded、NotReady 等明确结果。失败不占用 ID；Accepted 在整个会话保留去重标记，直到 Clear/重新 Prepare。Pending、驻留槽和会话 ID 容量分别配置、预分配并统计；桶占用不能只看当前存活数。预热后禁止按事件创建闭包、临时字符串或容器。

普通输入要求时间有限且非负，坐标有限；请求 SpawnTime 不得小于最后 Tick 的时间，排序键不得落在已提交事件键之前。Tick 截止后、时间等于上次 Tick 且键仍递增的请求可在下一帧处理；不允许借此向旧透明序列插队。未来出生请求保留到 due；本轮 due 请求按 `(SpawnTime, EventId)` 排序。Clip 正常跨帧派发天然满足时间约束，慢帧所跨区间的旧出生时间仍大于前次 Tick，不属于迟到。

首版有限 Clip 最大时长设为可版本化配置，默认 1,800 秒；准备阶段校验事件和结束时间不超范围。持续业务运行需要另定义时间原点屏障，首期不暗中重置时钟。

替代方案：复用 latest-wins 会丢独立事件；每次 ReplaceMembers 会重建成员及双份 renderer；无界 ID 集合会使长会话内存不可控，因此使用独立、有界的事件管理。

### 2. 图集与只执行一次的整串布局

`DamageTextGlyphAtlas` 保存固定纹理、0–9 的 UV/size/bearing/advance、参考像素字号、资源版本与哈希。Sample 使用固定、可追溯的数字图集，现有字体资产可作为制作来源；生成在编辑期或 Preparing 完成，不在采样中请求动态图集。字体许可、贴图导入、过滤、padding 和颜色空间与资源一起记录。

金额由复用的最多 8 位缓冲按除法/取模拆分。布局先累计 advance 和显式 tracking，再依据整个布局区间中心平移所有 glyph；宽度、bearing 与末字边界的约定在 atlas 版本中固定，用非等宽测试字形验证。局部顶点保留 float，缓存到事件槽；脏桶重填只复制缓存结果。字号相对参考字号的缩放、三模式颜色/寿命和动画参数冻结到事件快照。

替代方案：运行期 UGUI/TMP 排版和完整金额 Mesh 字典分别增加持续生成成本或组合存储；首版只缓存 10 个 glyph 和每个已接受事件的布局。

### 3. Float32 顶点协议与共用动画语义

首版单 stream，按 Unity 属性顺序组织，C# struct 使用明确顺序和尺寸；查询实际 stride/offset 并与 Shader 输入校验。建议协议 v1：

| 语义 | 格式 | 字节 | 内容 |
| --- | --- | ---: | --- |
| Position | Float32×2 | 8 | 相对整串中心的参考像素 XY |
| Color | UNorm8×4 | 4 | 颜色/不透明度 |
| TexCoord0 | Float32×2 | 8 | 图集 UV |
| TexCoord1 | Float32×4 | 16 | 世界锚点 XYZ、SpawnTime |
| TexCoord2 | Float32×4 | 16 | Lifetime、字号比例、预计算横向位移、上浮距离 |
| TexCoord3 | Float32×4 | 16 | 弹出幅度、弹出时长、淡出起点比例、Kind |
| 合计 | | 68 | 首期不量化 |

每串参数复制到所有字符顶点，避免首期引入参数表/SSBO。Kind 仅为 0/1/2 的精确分类值，不参与位置编码。横向位移由固定 uint 哈希从 RandomSeed 推导，在出生时计算；不调用渲染器随机流。

同一个可复用 HLSL 函数接受布局、事件快照、PlaybackTime、相机与视口。生命周期为 `0 <= age < Lifetime`；弹出在前段完成后回到 1，上浮/漂移按归一化年龄变化，末段平滑淡出。普通/暴击/加血采用需求默认颜色、1.0/1.4/1.0 相对字号和 0.8/1.0/1.1 秒寿命；精确曲线与参数写入样式版本。CPU 的生命周期及动画参考函数使用同一公式，供单步/校验和后继 C 适配。

先将世界锚点投影，再将整串局部像素偏移乘相同缩放及 clip.w 加到裁剪空间 XY；使用目标相机实际 viewport/RenderTexture 尺寸，不依赖 Screen 全屏尺寸。处理 GPU 投影约定和近裁剪面：锚点在相机后方或近裁剪面内时整串退化，避免除以近零 w。未来出生和已过期事件同样退化为零面积几何，不能只降低 alpha。

格式规则依据 [VertexAttributeDescriptor](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Rendering.VertexAttributeDescriptor.html)。理论满桶 4,096 顶点为 272 KiB，UInt16 的 6,144 索引为 12 KiB，每个 mesh 共 284 KiB；front/back 为 568 KiB，不含预留空桶、CPU 缓存、对象和材质。此为容量计算，不是测得显存。

### 4. 持久桶、单次上传与原子发布

桶容量默认 128，可配置且校验最大顶点/索引范围。事件仅追加到逻辑尾部尚未满的桶；如果尾桶已回收则建立新的逻辑尾桶。物理池槽复用时分配新的单调逻辑序号，禁止按物理池编号重排。满桶封闭；不向旧桶空洞回填。

一个主线程 Tick 形成帧截止点：取走截止前 pending，排序，布局一次，准备 dirty 桶 back。一个 Unity 帧只提交一次 Tick；重复同帧调用幂等，倒退时间和重入明确拒绝。截止后请求留到下一帧。Tick 期间禁止重入 Spawn/Prepare/Clear。

所有本截止点脏桶的 back、有效索引范围及资源绑定准备成功后统一交换。任一阶段失败时，所有旧 front 保持有效，已接受事件及布局保留，计数不冒充首次发布，进入 Failed，显式 Recover 后从已提交状态和 pending 重建。首次准备失败无可绘制输出。资源失效导致旧 front 不安全时清除绘制命令；更换资源/样式须新版本屏障或重新 Prepare，不在普通 Tick 里隐式更换。

每脏桶每截止点最多一次合并/上传；同帧跨 127/128/129/256 事件不能变成每串上传。稳定 Tick 和相机运动只更新时钟/诊断，不写顶点或索引；回收全空桶可修改 DrawRevision，无须重填其他桶。

替代方案：局部范围分配/压缩会增加首期复杂度；先接受小桶重填和双份几何，并分别记录布局次数、重填次数、上传次数与理论 API 字节。

### 5. GPU 隐藏、全桶回收与容量

CPU 和 GPU 共享 `[SpawnTime, SpawnTime + Lifetime)`。字符过期后在仍有存活事件的桶中保留，计为 expired-resident；整桶所有事件过期即从逻辑绘制序列移除并归还池。尾部混合寿命可能保留死字符，因此同时记录 live/submitted/expired-resident 字符、逻辑桶/有效 Draw、占用率、池容量与 front/back 数量。

慢帧中完整出生并结束的事件仍完成消费和生命周期计数；无首次发布者记 missed-presentation，不为其上传无意义的几何。相机裁剪单列，不能与慢帧漏显、输入拒绝或上传失败混成一个“零存活”。正式功能/性能验证要求拒绝数为 0，非法输入专用用例除外。

Clear/停止/Dispose 先移除相机命令和订阅，再释放自有 mesh/material/缓冲与会话状态；反复调用安全，不销毁外部 atlas/样式/相机。普通全桶回收只归还会话池；下一次 Prepare/跳转所需资源重新预热，成本归到恢复阶段。

### 6. 每帧参数与有序 Built-in 提交

渲染器实现现有 `IHolderDrawSource`，按逻辑桶及桶内索引顺序记录 front。默认 `ZTest Always / ZWrite Off / Blend SrcAlpha OneMinusSrcAlpha`，三模式共用 atlas、材质与一个 pass。

在单一指定 Built-in 相机的独占 HUD 事件，组合“每帧参数命令”与现有 `HolderBucketCameraPass` 绘制部分：参数前缀每帧更新 PlaybackTime、GPU 相机矩阵、近裁剪参数和 viewport；绘制命令仅在 DrawRevision 变化时重录。参数缓冲在绘制缓冲之前注册/执行，pre-cull 校验确保相机最后状态有效。使用专用全局 uniform 名称，避免材质/MPB 捕获旧时间或覆盖前缀；所有权由一个适配器统一管理。

首期采用直接相机命令提交与 Shader 锚点裁剪，不使用基于局部 POSITION 的 renderer/frustum 筛选。Mesh 显式赋 Bounds，描述保守锚点/动画范围；提交策略不依赖自动 RecalculateBounds 推断 Shader 位置。远锚点、视口边缘、相机移动及 RenderTexture 均需图形测试。SRP 显式不支持，不挂错 pass。

### 7. Clip、播放时钟与恢复

`DamageTextClip` 在准备阶段验证唯一 ID、范围、排序、版本、资源引用和相机轨迹。内容哈希基于固定字段顺序/字节序和有限浮点位模式；记录生成算法版本及种子，渲染器不再次生成事件。哈希不依赖本地化 JSON 文本、路径或字典遍历。

前进时用单一 double 播放时钟累计，统一转换为 Shader/请求使用的相对 float 时间；首版遵守最大时长。派发区间 `(previousTime, currentTime]`，初始化独立处理起点事件。暂停保持相同时钟继续绘制；固定步长和固定时间序列由控制器提供时刻，不各自使用 deltaTime/_Time。相机轨迹也从同一时刻求值。

Seek 一律 Clear + Prepare，重建目标时刻存活事件并定位下一个派发游标；起点、死亡边界与排序与正常回放一致。恢复后的逻辑存活/动画状态一致，但物理桶历史可能不同，记录 recovered 状态，不作为正常运行基准检查点。性能固定状态必须正常播放到检查点再冻结。

### 8. 交互与统计隔离

提供播放/暂停/重启、时间轴/指定时间、单步、速度、Clip、渲染方案选择。首期只有 B 可选；后继方案显示明确可用状态。基础面板降频刷新，展示时间、存活/提交/驻留字符、桶/Draw、接受/拒绝/漏显及出生/回收。

状态 Idle → Preparing → Warming → Sampling → Completed，错误进入 Failed；预览暂停为 Paused 并记返回状态。正式采样中暂停、单步、跳转、改速度/Clip/方案/配置均终止当前段，导出 aborted 原因，再进入预览或新段；不恢复后拼接旧段。记录器与 UI 分离，元数据包括代码/源码指纹、Clip/配置/样式/图集哈希、设备/API/构建和采样模式。

## Risks / Trade-offs

- [持续出生与混合寿命造成过期驻留] → 先量化 submitted/live 差异和桶占用，后续根据证据评估封桶或压缩。
- [68 B 顶点与 front/back 增加带宽/内存] → 保存真实 stride 和容量口径，M5 单独压局部 XY；不提前宣称 GPU 受益。
- [参数缓冲顺序或缓存导致时间冻结] → 同一 DrawRevision 下验证时间推进、暂停与相机运动，实际图形设备核对。
- [失败后事件已接受却未显示] → 保留队列/ID/容量预留，明确首次发布与恢复统计；不静默重放两次。
- [动态图集或配置改变历史事件] → 固定资源版本，显式准备/屏障，采样变更终止段。

## Migration Plan

1. 完成 M1 的纯事件/布局、固定图集/样式和 Float32 Shader；先验静态输出及动画公式。
2. 完成 M2 的 Clip/时钟/控制，接入一个 B 方案；用简单有界几何验证回放语义。
3. 完成 M3 的预热持久桶、原子发布和相机适配，替换首期临时几何维护；完成图形与资源回归。
4. 运行既有布局、治理、EditMode 和实际图形 PlayMode；记录精确源码状态与失败/跳过。此阶段不宣称设备性能达标。
5. 后继对照复用冻结的事件/布局/Shader 语义；若回退，Sample 移除新入口并 Dispose，既有 holder API 无需迁移。

## Open Questions

最大业务并发/出生峰值、正式预算、精确字形与动画、美术接受阈值、业务深度/API/URP 与真实 ECS 源码仍待提供。首期使用明确、可版本化默认完成可执行基线，不把 128 桶/256 爆发、1,800 秒或 1.5 ms 参考写成业务承诺。
