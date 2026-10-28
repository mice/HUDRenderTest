# 战斗飘字与确定性播放器

首期入口为 `DamageTextRenderer`、`DamageTextPlayback` 和 Sample 场景 `HUDRenderTestSample/Assets/Scenes/Scene_DamageTextPlayback.unity`。适用于 Unity 2022.3、Built-in 单一指定 HUD 相机；三模式共用固定数字 atlas 和一个普通 Alpha pass。实例化、FP16、真实业务 ECS 和设备性能排名由后继 `compare-damage-text-renderers` 负责。

## 使用方式

打开 Sample 场景即可预览 `Boundaries`、`Continuous`、`Bursts127_128_129_256` 三段记录。控制面板提供播放、暂停、重启、单步、时间轴/指定时间跳转、速度、Clip 和方案可用状态；首期可用方案为 B：持久 Mesh。面板统计可关闭，并以 5 Hz 刷新，不与渲染时钟混用。

独立接入时使用固定 `DamageTextGlyphAtlas`、`DamageTextStyles`、版本化 `DamageTextClip` 及 `UIData/DamageText` Shader：

```csharp
var playback = new DamageTextPlayback();
playback.Prepare(clip, new DamageTextConfiguration(), atlas, styles, shader, camera);
playback.Play();
// 每个播放帧只从一个驱动调用，elapsedSeconds 由调用者提供。
playback.Advance(elapsedSeconds);
playback.Pause();
playback.Step(1f / 60f);
playback.Seek(0.5f);
// 在拥有者停止/销毁时调用。
playback.Dispose();
```

实时事件可直接调用 renderer 的 `Prepare / Spawn / Tick / BindCamera / Clear / Dispose`。`Tick(time)` 使用 Unity 帧号；`Tick(time, frame)` 为固定序列/测试提供显式、单调的帧号。`Spawn` 成功表示接受与容量预留，成功 Tick 后才算首次发布。

## 输入、快照与时钟

- Amount 范围 10–99,999,999；不补零、不带加号/负号/单位。Normal/Critical/Healing 默认寿命 0.8/1.0/1.1 秒；颜色、字号、动画在 Prepare 冻结，编辑后重新准备。
- 每事件按值复制 EventId、Amount、Kind、WorldPosition、SpawnTime、RandomSeed。已结束 EventId 在同一会话仍去重；拒绝不预留 ID。缺字形、非法输入、迟到与容量溢出均显式返回并分类计数。
- 截止前请求按 `(SpawnTime, EventId)` 排序；截止后请求下一帧处理。未来请求保留到出生，禁止向已提交透明序列插队。Renderer 不接受时间倒退；播放器 Seek 清理并恢复目标存活集合。
- Seek 要求当前 atlas/style 哈希与会话快照一致；编辑后显式 Restart，不能在恢复历史时切换样式。Restart 重新校验 Clip/字形/容量和事件结束时间；新寿命超过 Clip 时长会明确拒绝。
- Clip 使用固定字段顺序、小端二进制内容哈希；返回事件/相机 key 的值，构造后修改输入数组不改变 Clip。相机轨迹和 GPU 使用同一播放时刻，不读取 Shader `_Time`。
- CPU/Shader 均以相同 float 的 `time >= spawn && time < spawn + lifetime` 判断寿命。上界为舍入后的 float 和，避免较大时间下加法/减法两种比较在最后一帧分歧。double 播放时钟统一转换到该 float 时刻。
- 默认最大相对时长 1,800 秒，明确校验事件结束时间；本版本不提供无限实时会话或自动时间原点迁移。

## 几何、排序与容量

默认每桶 128 串、8 个桶；每串最多 8 glyph。配置分别约束桶、pending 和会话 EventId。事件槽/布局、几何缓冲、双份 Mesh 和会话 ID 集合在准备阶段预分配。池中已过期但尚未整桶回收的事件仍占容量，因此不能只以 live 数判断可接受规模。

每事件只布局一次；追加时重填 affected 小桶，保留缓存布局。一个截止点的全部脏桶 back 上传完成后统一发布，任何异常保持旧 front 和 accepted pending，进入 Failed，`Recover()` 在同一冻结时间重新构建并发布。新资产/版本失效必须重新 Prepare，不能普通 Tick 中隐式改图集。

不向旧桶空洞回填，复用物理槽时放到逻辑尾部。无出生时，时间/相机推进不重填或上传几何。过期字符在 Shader 退化为零面积，仍有顶点执行/存储；全桶结束后从 draw 集合移除并归池。`live / submitted / expired-resident` 必须一起解释。

首期每顶点 68 B：Position Float32×2、Color UNorm8×4、UV Float32×2、三组 Float32×4 参数。Prepare 查询 stride 及 0/8/12/20/36/52 属性 offset，UInt16 索引仅提交有效字符范围。满 128 串、8 位桶的一份缓冲理论为 272 KiB 顶点 + 12 KiB 索引；front/back 合计 568 KiB，另有池预留、CPU 状态/布局与对象开销。这些是容量值，不是实测 GPU 内存。

## 相机与资源所有权

使用现有 `IHolderDrawSource / HolderBucketCameraPass`。同一 `AfterForwardAlpha` 事件中，参数前缀每帧在缓存 Draw 前设置播放时间、GPU 投影、view、viewport 与 near；像素 Y 与 GPU 投影方向保持一致，包含 D3D RenderTexture 翻转；DrawRevision 不变仍能动画。默认 ZTest Always、ZWrite Off、SrcAlpha/OneMinusSrcAlpha，后出生覆盖前出生。

直接相机命令不依赖局部字形 POSITION 的 renderer/frustum Bounds；Mesh 有显式保守 Bounds。Shader 根据世界锚点和相机投影处理像素布局及后方/近端退化。SRP 明确拒绝；多相机与外部透明物体交错排序没有首期契约。

`FirstPublishedTime` 是 CPU 成功发布时刻。`FirstCameraEligibleTime / CameraEligibleEvents / NearPlaneClippedEvents` 只反映最近指定相机 pre-cull 的前方/近端条件，不等于实际像素可见，也不证明没有屏幕外裁剪或遮挡；没有相机观察时该时间为 -1。慢帧内完整出生并结束的事件计 consumed/ended/missed-presentation，不上传无意义几何。

Clear/Stop/Dispose 先移除两个命令缓冲和 pre-cull 订阅，再释放自有 Mesh、材质与事件状态；普通整桶回收保留预热池。外部 atlas/样式/纹理/相机不被销毁。Seek 会重新准备，恢复耗时和桶历史与正常回放不同；固定状态性能检查点应正常回放到达再冻结。

## 采样段与报告

状态包含 Idle、Preparing、Warming、Sampling、Paused、Completed、Failed。`BeginSampling` 预热后开始正式段；暂停、单步、Seek、速度/Clip/方案/配置变更或停止中止当前段。预热中断也保留 aborted 原因，后续预览不拼接旧段。

Sample 将段报告/手动状态保存到 `Application.persistentDataPath/DamageText`。记录 Clip/atlas/style/config 哈希、代码/dirty 状态/源文件指纹、Unity/设备/API/构建、相机姿态/投影/视口、速度、恢复状态及统计。Editor 在每次准备 Clip 时刷新源码信息；Player 使用创建/构建场景时固化的字段，构建前可执行 `DamageTextSampleBuilder.StampMetadataBatch` 更新。当前报告范围是 `functional-playback`，GPU/GC 为 unavailable；没有声称 HUD GPU 或 Android 零 GC。当前设备预算及 ABBA 性能实验尚未由此功能报告验证。

## 验证与追溯

```powershell
.\Tools\UnityTestRunner\Sync-UnitTestGovernance.ps1 -Scope All
.\Tools\Invoke-LocalValidation.ps1 -WithGraphics
.\Tools\Invoke-LocalValidation.ps1 -WithGraphics -WithCoverage -CoverageThreshold 70
```

`-WithGraphics` 为 PlayMode 使用 D3D11；默认脚本仍使用 null graphics 做快速行为回归。图形测试跳过不算像素验收。正式结果以仓库 change 中的执行证据为准，测试记录索引只说明测试存在。

| 要求 | EditMode | 实际图形 PlayMode |
| --- | --- | --- |
| FT-01–06 / AC-01 输入、边界、布局、快照 | UT_DAMAGE_001–005、014、023/024 | UT_DAMAGE_105–107：三模式、固定 atlas 和 RenderTexture 字形方向 |
| FT-08–12 / AC-02 统一时钟、动画、暂停、Seek | UT_DAMAGE_015–019、022 | UT_DAMAGE_102/104：缓存时钟及相同像素 |
| AC-03–05 持久桶、单次上传、顺序 | UT_DAMAGE_006–008 | UT_DAMAGE_101/103：128/129 顺序、viewport、远锚点 |
| FT-07 / AC-06 寿命、回收、资源 | UT_DAMAGE_008、012/013、018、020 | UT_DAMAGE_102/105/108：结束无残影、过期驻留继续绘制、反复释放 |
| AC-07 失败与恢复 | UT_DAMAGE_009–011、021 | 已发布前后由包故障注入核对；缓存相机输出由 101/102 覆盖 |
| AC-08 首期 Float32 Bounds | UT_DAMAGE_006：stride/索引 | UT_DAMAGE_103：近裁剪、远锚点、viewport |
| AC-09–10 基础追溯和采样隔离 | UT_DAMAGE_014、017/019 | UT_DAMAGE_106：正常回放所有 Sample Clip，无拒绝/漏显 |

FP16/设备性能/GPU/真实业务 ECS 尚属后继范围；现有 null-Sprite 分桶的 1.5 ms P95 不能当作本功能已通过的预算。
