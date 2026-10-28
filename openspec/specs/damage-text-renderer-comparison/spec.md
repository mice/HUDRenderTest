# damage-text-renderer-comparison Specification

## Purpose

规定数字渲染方案的等价范围、分阶段采样、指标有效性与可复现设备对照，保留 GPU 和完整帧结论各自的验收边界。

## Requirements

### Requirement: Comparable renderer adapters with explicit scope

系统 SHALL 对 A 共享 Quad 字符实例化与 B 持久数字 Mesh 使用同一 Clip、布局、样式、图集采样、时钟、相机、像素覆盖、顺序、深度/混合/裁剪和 Pass 数。实例化 SHALL 显式校验平台/材质/分组容量，不能截断超上限字符。C 通用 holder SHALL 仅在画面等价时参加排名，否则记录 excluded/reason；未取得真实业务代码时 D SHALL 为 not-integrated，不添加 Entities 构造替身。

#### Scenario: Instance group boundary and 128 eight-digit strings
- **WHEN** A 渲染 1,024 个字符并跨越实际 API 分组边界，与 B 同时刻参考比较
- **THEN** 无字符截断、重排或重叠像素差异，记录实际实例组/Draw；排序不等价时不得直接排名

#### Scenario: Generic or business adapter is unavailable
- **WHEN** C 无法保持同画面，或 D 缺业务 ECS/Instantiate 源码
- **THEN** 报告明确排除/未接入原因和接入条件，不将 A 命名为真实业务 ECS

### Requirement: Separate visual fixed-sequence and realtime modes

系统 SHALL 区分画面检查、固定时间序列与实时压力。画面检查 SHALL 允许双画面，正式计时 SHALL 同时只启用一个方案。固定序列 SHALL 使用相同预注册时刻；实时压力 SHALL 消费相同事件区间并记录各自真实帧/时间、pending/拒绝/漏显。暂停/跳转/速度/方案/配置改变 SHALL 终止当前正式段。

#### Scenario: Fixed times and slow realtime frames
- **WHEN** 固定序列对比，或实时运行中 A/B 帧速不同
- **THEN** 前者逐时刻核对存活/动画/顺序，后者核对相同请求与完整生命周期并保留慢帧漏显，不将不同帧边界冒充逐帧同画面

#### Scenario: Side-by-side preview
- **WHEN** 开启双画面对照
- **THEN** 记录为视觉检查，CPU/GPU 不纳入正式 A/B 性能结果

### Requirement: Distinct GPU-only and full-lifecycle phases

系统 SHALL 分别测量预备几何/实例后的纯绘制阶段和包含出生/布局/队列/重填/上传/回收/提交的完整生命周期阶段。固定 GPU 检查点 SHALL 从正常回放到达后冻结，保留真实桶历史，不能通过 Seek 消除过期驻留。Shader 时间暂停 SHALL 继续绘制，准备/恢复/屏障成本 SHALL 标识并单列。

#### Scenario: Freeze a normally reached checkpoint
- **WHEN** 正常回放形成混合寿命/历史空洞后冻结
- **THEN** A/B 使用同一逻辑存活状态，B 的真实 submitted/expired-resident 仍记录，不通过重新紧凑布局降低工作量

#### Scenario: Faster draw but more expensive lifecycle
- **WHEN** B 的 GPU 绘制改善但出生/维护导致完整帧没有改善
- **THEN** 分开报告两阶段结论，不据 GPU 结果宣布业务替换成功

### Requirement: Traceable complete metrics with validity flags

每段 SHALL 保存代码/源码状态及指纹、构建/设备/API、Clip/配置/atlas/style/camera 哈希、事件序列校验和与原始报告哈希。CPU SHALL 区分事件/布局/维护/上传/提交、主线程/渲染线程；GPU SHALL 标明 HUD 范围或 whole-frame，缺可靠数据 SHALL unavailable。GC、内存和上传 SHALL 分清可用性、理论值/对象估计/实测值，不能用零或 CPU Stopwatch 代替缺失 GPU。报告 SHALL 包含 live/submitted/expired-resident、真实 stride/索引/Draw/Pass/桶/池/front-back、延迟/拒绝/漏显。

#### Scenario: Missing GPU or allocation counter
- **WHEN** 设备无法提供 HUD GPU 时间或可信 GC 计数
- **THEN** 相应字段和结论保持 unavailable/unverified，不输出 0 ms/0 B 或由 CPU 推算的 GPU 收益

#### Scenario: Sparse rebuild frames
- **WHEN** 大部分帧没有出生，只有少量帧上传
- **THEN** 同时报整段和真实刷新帧 median/P95/P99/max/n，上传理论 API 字节不冒充驱动实传

### Requirement: Balanced device capture and honest conclusions

首轮 SHALL 使用 Mi 10、Release IL2CPP ARM64/Vulkan 的已记录配置，先 128 串 2/5/8 位，再连续伤害、127/128/129/256 爆发及低/高重叠。A/B SHALL 使用预注册预热与 ABBA 平衡顺序，每正式运行至少 3,000 个有效采样帧，爆发至少 100 个完整请求窗口；不足 SHALL 标记无效/不足并统一重跑。重型抓帧/诊断 SHALL 与计时分离。结论 SHALL 分 GPU、完整帧和统计差异；1.5 ms 仅为探索参考，其他 API/业务替换/RC 验收不得外推。

#### Scenario: Incomplete or unequal run
- **WHEN** 出现拒绝、事件序列不同、画面失配、样本不足或配置/源码无法追溯
- **THEN** 分析器拒绝直接排名，保存原因与原始数据，不删除长尾或增加空帧达标

#### Scenario: Vulkan candidate is measured
- **WHEN** 完成可追溯的平衡测量
- **THEN** 保存分位数、最大值、样本数和重复波动，按已验证范围给结论，OpenGLES3/WebGL/URP/D 仍按各自证据状态标记
