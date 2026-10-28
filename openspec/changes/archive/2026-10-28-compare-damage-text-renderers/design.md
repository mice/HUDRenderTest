> 2026-10-28：本设计随阶段交付归档；实现结果和未完成的正式 GPU 验收见 [归档记录](archive-record.md)。

## Context

本设计承接[首期设计](../2026-10-26-add-damage-text-playback/design.md)的固定事件、布局、样式、动画和持久桶。依赖条件是 M1–M3 功能/资源验证通过；不能在基线仍有漏派、排序或生命周期错误时用性能结果比较架构。

立项时只有通用 HUD 的 Mi 10/Vulkan 证据；本 change 不预设 A/B 胜负，也不把 A 称为业务 ECS。依据和待确认事项见差距审查（原始工件仅本地保存）。

## Goals / Non-Goals

**Goals:** 以相同画面/输入建立 Float32 A/B；分开纯 GPU 与完整生命周期；提供可追溯的固定序列/实时压力记录；单独验证 B 的 Float16 局部 XY，并给出接受/回退结论。

**Non-Goals:** 用模拟 Entities 代替真实业务；替换业务代码、URP/多 API 推广、主城需求实现或宣布 RC/正式发布通过。

## Decisions

### 1. 方案协议与同画面门禁

沿用首期事件/时钟/布局协议，以明确的方案接口封装 Prepare、Spawn、Tick、Clear/Dispose、DrawSource 与诊断快照。播放器拥有输入和相机；每方案只管理自己的渲染数据。先使用 Float32，单一正式采样段只启用一个方案。

| 方案 | 实施选择 | 可比较范围 |
| --- | --- | --- |
| A | 一张共享 Quad；每字符实例参数包含相同 glyph 局部位置/尺寸、UV、颜色、世界锚点、出生和动画参数；使用预分配数组/MPB 与有序实例化命令 | 与 B 对照渲染及真实数据维护；不等同业务 ECS |
| B | 首期持久数字 Mesh，保持真实桶历史与过期驻留 | 主要候选；Float32 为初始默认 |
| C | 检查通用 holder 的同布局/图集适配，必要时以同公式在 CPU 更新几何，并完整计入其成本 | 通过图形/状态等价才直接排名，失败明确 excluded/reason |
| D | 真实业务 ECS 未接入 | 报告 not-integrated，不添加 Entities 伪造基线 |

A 的命令通过相同 Built-in 相机事件提交。事件/字符按固定顺序连续分组，不按 Kind/纹理回填；分组容量遵守所用 API 和目标平台的有效上限，不能为了一个 Draw 裁掉 128×8=1,024 个字符。使用 `CommandBuffer.DrawMeshInstanced` 的可行性、支持查询、材质 instancing 和数组/Shader 容量在实施时验证。[命令 API](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Rendering.CommandBuffer.DrawMeshInstanced.html)

Unity 不自动对实例化组中的每个实例进行透明排序；按已排序输入、分组边界和重叠像素进行实际设备校验。失配时改为能保持顺序的有序分组并报告真实 Draw/成本；无法满足则该方案不具备直接排名资格。不能通过降低 B 透明度、改混合或改成 AlphaTest 来匹配 A。[实例化排序说明](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Graphics.DrawMeshInstanced.html)

共享 HLSL 动画/投影函数，A 的实例解包与 B 的顶点解包输出同一语义输入。图集纹理/过滤/颜色空间、像素字号、曲线、可见集合、深度/混合/裁剪、pass、分辨率与相机完全一致；shader variant 和实例上传 API 成本如实计入。缓存策略按等价原则应用，记录不可避免的架构差异，不能人为关闭一侧可用缓存。

### 2. 三种模式与两阶段测量

| 模式 | 时钟 | 结果用途 |
| --- | --- | --- |
| 画面检查 | 暂停/单步/拖动；允许双画面 | 视觉、顺序、边缘、相机与误差；不纳入正式计时 |
| 固定时间序列 | 预注册的同一采样时刻列表 | 同事件/几何状态的精确成本比较，不能当实时 FPS |
| 实时压力 | 同一 Clip 按正常速度消费 | 慢帧、积压、拒绝/漏显和整帧成本；记录各自真实时刻/帧映射 |

阶段一：预备实例/几何，以正常回放到达固定检查点再冻结，或选择无出生/回收区间的同步序列。采样不含准备/生命周期重建，但仍报告保持画面必需的帧参数、绘制提交和 GPU；提交/驻留字符必须记录，不能用 Seek 消除 B 的历史空洞。

阶段二：消费完整相同事件区间，包含布局、创建/回收、队列、dirty 重填、Mesh/实例上传、命令提交和真实维护。固定序列逐时刻检查状态 checksum；实时模式检查请求序列、最终消费/结束及当帧存活 oracle，允许各自帧边界不同，不假装逐帧输出时间相同。慢帧漏显不消失，出现拒绝/输入不等价即标记运行无效。

准备/预热、采样、恢复/配置屏障各自标记；全帧成本和事件区间仍保留。预热时长/方法在运行前固定；实际采样不足 3,000 帧时标记不足，统一延长/重复预注册负载后重跑，不填入空负载帧伪造样本量。暂停/改变配置一律终止正式段。

### 3. 最小矩阵与设备捕获

先跑 128 串：分别 2/5/8 位、固定三模式混合；再跑连续出生/结束、127/128/129 和 256 同帧爆发、相同负载的低/高重叠。FP16 在已验证 B Float32 上单变量执行，避免所有维度笛卡尔积。

第一设备 Mi 10，Release IL2CPP ARM64/Vulkan；记录温度、电源、分辨率、帧率目标、驱动和图形 API。ABBA（A/B/B/A）分别捕获纯绘制和生命周期，每个正式运行至少 3,000 个有效采样帧，爆发至少 100 个完整请求窗口。预先声明异常样本处理，不因长尾难看删除；保存 median/P95/P99/max/n 与重复波动。

Frame Debugger、重型抓帧、像素读回与细分阶段诊断在独立验证轮完成，正式轮只用轻量外层计时。真实业务 API、OpenGLES3、URP 和 WebGL 没有证据时各自标记未验证，不能外推 Vulkan。

### 4. 指标与可追溯记录

Sample 的 BenchmarkRecorder 与控制 UI 分离，预分配采样数组，结束后再写文件。沿用现有源码/APK/报告清单思路，新增数字负载 schema 和离线分析；复用脚本可用部分，不沿用只支持 null-Sprite holder 的配置。

| 范围 | 字段/口径 |
| --- | --- |
| 输入 | Clip/配置/atlas/style/camera 哈希、采样时刻表、事件请求与消费 checksum、scheme/version |
| CPU | 事件验证/布局/入队/生命周期、脏桶重填、Mesh/实例上传 API、帧参数、命令记录/提交、主线程/渲染线程与计数器可用性 |
| GPU | HUD 范围 GPU 毫秒、工具/范围/计时有效性；仅整帧可得时标为 whole-frame，不写成 HUD；不可得则 unavailable |
| 事件 | accepted/rejected/pending/consumed、首次发布/进入可绘制集合、结束、漏显/裁剪、待处理年龄、故障/恢复 |
| 几何 | live/submitted/expired-resident 字符、实际源顶点/索引、stride、有效 Draw/pass、逻辑桶/池/占用率、front/back、A 实例数和分组 |
| 存储/分配 | CPU 缓存/队列、Unity 对象估计、理论 GPU 缓冲、工具测得 GPU 内存分列；GC 的预热/采样/回收与有效性标志 |
| 环境 | commit、dirty 状态/源码清单指纹、包/Unity/设备/API/后端/构建、分辨率、温度、时区/时间及原始工件 SHA256 |

稀疏更新的实际刷新帧另有分布，并同时报告整段。理论上传字节按提交 API 输入计算，单独命名，不当作驱动实传量。GPU 和 CPU 并行时间不直接相加。IL2CPP 不调用项目已知不可用/有崩溃风险的线程分配 API，按现有 runner 的可用性策略报告。

报告有效性门禁先检查输入/视觉/状态、容量/拒绝、样本数和可追溯性，再讨论性能。最终分别给出 GPU 绘制改善、完整帧改善、差异不显著；证据不足输出 unverified/inconclusive，不硬套排名。1.5 ms 仅为 Mi 10/128 串更新 CPU 的探索参考，不代替同负载正式预算或 RC 验收。

### 5. Float16 只压局部 XY

B 的 v1 顶点中仅 Position 由 Float32×2 改为 Float16×2；局部布局中间值、世界 XYZ、SpawnTime、UV、样式/随机参数与投影计算保留原精度。Preparing 查询 `SupportsVertexAttributeFormat(Float16, 2)`，同时验证实际描述/struct stride/offset；不支持时选择 Float32 并记录 requested/effective/reason。不能只改 Shader half。[能力查询](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SystemInfo.SupportsVertexAttributeFormat.html)

理论 stride 68→64 B，4,096 顶点减少 16 KiB，每个满桶 front/back 合计减少 32 KiB 顶点容量；索引不变。各 API 实际存储/带宽另测，不能把 5.9% 理论顶点缩减当作 GPU 收益。

保存量化前后局部坐标，用相同动画/相机/viewport 投影求最大屏幕位置差，并在实际图形设备做像素/边缘回归。覆盖 2/8 位、声明最大字号/暴击峰值、非整数度量、相机运动、透视/正交、远锚点与视口边缘。候选阈值 ≤0.25 px；不满足即 Float32 或另开带版本的归一化实验。通过精度阈值仍需美术接受与同负载测量，未确认前保持 Float32 默认。

沿用首期直接提交/Shader 裁剪和显式 Bounds；不能对 Float16 Position 调用 RecalculateBounds 期待自动正确。格式切换在新准备/采样段生效，禁止同段混入隐式回退。[Bounds 限制](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Mesh.RecalculateBounds.html)

## Risks / Trade-offs

- [目标设备没有可靠 HUD GPU 工具] → 继续完成 CPU/生命周期与报告，保留 GPU 未验证任务，不能将缺指标当作完成该门禁。
- [实例化排序/分组改变覆盖] → 先过真实图形门禁，记录分组；失败不直接排名。
- [C 的 CPU 动画不等价或代价大] → 保留完整成本/排除原因，不把 C 改造成第二套专用数字顶点后仍称通用路径。
- [量化节约被其他属性/前后缓冲淹没] → 同一 Float32 基线单变量比较，保存完整 stride/内存和统计波动。
- [持续压力的真实帧边界不同] → 固定序列用于同状态，实时用于端到端；分别报告，不拼接结论。

## Migration Plan

1. 核对首期功能证据和固定 Float32 基线，增加 A/候选 C 方案与画面门禁。
2. 完成采样模式、报告、分析器和正向无效数据用例，再验证设备工具范围。
3. 以预注册矩阵执行 Float32 ABBA，保存同源构建与原始数据；形成 GPU/完整帧分别判断。
4. 新段启用 FP16，先精度/回退/边缘验证，再做与 Float32 的平衡重复测量。
5. 更新结论/限制与 M6 接入条件。归档首期或本 change 时修正跨 change 链接，保留需求快照哈希。
6. 回退只需选回 B Float32 并 Dispose 其他方案，不修改既有 holder/主城 API 或发布状态。

## Open Questions

设备 GPU 计时能力、正式业务预算/峰值、美术 FP16 接受性和真实 ECS/Instantiate 封装尚无证据。D 接入需业务源码、共享 Quad/缓存/池行为、相同 Shader/深度/裁剪、目标管线/API 及真实创建/系统成本。主城后续使用全局约 10 次内容更新/秒另建负载，不算每 HUD 10 次，也不重做已完成分桶。
