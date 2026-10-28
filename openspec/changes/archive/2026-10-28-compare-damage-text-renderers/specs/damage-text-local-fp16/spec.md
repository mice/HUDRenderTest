## ADDED Requirements

### Requirement: Local-only explicit storage format

B 的 FP16 实验 SHALL 只将相对整串中心的 Position XY 由 Float32×2 改为显式 Float16×2；布局中间值、世界锚点、出生时间、UV、其他参数及投影运算 SHALL 保持 Float32 基线语义。实际属性格式、维数、offset 和完整 stride SHALL 查询并记录，C# 缓冲布局 SHALL 匹配。只把 Shader float 改为 half SHALL 不算完成存储压缩。

#### Scenario: Compare full-bucket storage
- **WHEN** 相同 4,096 顶点使用 v1 Float32 与 FP16 布局
- **THEN** 查询并记录目标 68/64 B stride 及真实字节，理论局部字段差为 16 KiB/mesh，索引不变，不将其称为 GPU 加速比

### Requirement: Capability fallback and segment isolation

系统 SHALL 在 Preparing 查询 Float16×2 支持并验证顶点布局；不支持 SHALL 回退 Float32，记录 requested/effective/reason。格式选择、精度阈值失败后的回退或配置变更 SHALL 进入新准备/采样段，不得在同段混合结果。Float32 SHALL 保持初始默认，直到精度、美术和同负载成本证据满足接受条件。

#### Scenario: Unsupported device
- **WHEN** Float16×2 能力不可用
- **THEN** 使用等价 Float32 继续显示并明确记录回退，不能把该段标为有效 FP16 性能结果

#### Scenario: Quantization exceeds candidate threshold
- **WHEN** 最大屏幕位置差超过 0.25 px 候选门槛，或美术尚未接受
- **THEN** 默认保持 Float32，记录失败/待确认范围；归一化等后续实验使用新版本，不能静默改变基线

### Requirement: Screen-space precision and clipping validation

FP16 SHALL 相对同输入 Float32 记录最大屏幕位置误差，覆盖 2/8 位、声明最大字号、暴击峰值、非整数度量、透视/正交、相机运动、远锚点和视口边缘。图形验证 SHALL 使用实际设备，保留量化/投影数据与画面证据。Bounds/提交策略 SHALL 覆盖 Shader 动画位置，非 Float32 Position SHALL 不依赖 RecalculateBounds 自动推断。

#### Scenario: Maximum critical scale at viewport edge
- **WHEN** 8 位暴击在最大字号/缩放及移动相机下经过画面边缘
- **THEN** 报告最大位置差，无错误裁剪/消失，实际图形设备结果与参考边界一致

### Requirement: Single-variable performance experiment

Float32/FP16 SHALL 在相同事件、布局/Shader 语义、图集、相机、动画、桶历史、提交策略及设备配置下平衡重复；只改变局部 XY 存储。报告 SHALL 分开完整 stride/内存、理论/实测上传、GPU 与完整生命周期，并记录缺失指标。精度通过 SHALL 不自动等于性能改善或默认切换许可。

#### Scenario: No significant measured benefit
- **WHEN** 精度满足候选门槛但 GPU/完整帧差异未超过重复波动
- **THEN** 结论为差异不显著或保留适用范围，不将 16 KiB 理论节约宣传成整帧改善
