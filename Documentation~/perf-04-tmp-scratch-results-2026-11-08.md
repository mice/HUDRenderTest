# PERF-04：TMP 提取缓冲复用与 Mi 10 对照（2026-11-08）

当前记录：TMP 提取缓冲复用、Unity 验证及 Mi 10 九轮对照已完成，主代理独立重跑严格分析并验收。此结论仅关闭本轮子项，PERF-04 整体验收继续开放。

## 实施与范围

`TMPMeshDataUtility.Fill` 原来每次创建并释放 `VertexHelper`。现在在主线程复用一个已初始化 helper，退出时清空当前几何；嵌套提取使用独立临时 helper，异常通过 finally 清理。`ReleaseScratch()` 可重复调用，提取中的释放请求延迟到外层退出；子系统初始化、应用退出和编辑器重载/退出均注册清理。

`UITMPText.DoGenerate` 仍更新 Canvas 并强制 TMP 再生成，没有缓存文字几何或跳过变化文本。Managed 输出长度变化、容量增长和嵌套调用仍可能分配；保留 helper 高水位容量，释放后 UGUI 池也可能保留底层列表。多材质源几何测试不等于 fallback atlas/多材质批次支持，TMP-01 继续开放。

## Unity 验证与分配归因

| 验证 | 结果 |
|---|---:|
| 原项目完整 EditMode | 204/204 |
| 原项目 D3D11 PlayMode | 64/64 |
| stock UGUI 相关 EditMode | 12/12 |
| stock UGUI 性能 runner PlayMode | 11/11 |
| 测试治理 | 265/265 |
| Package layout / OpenSpec strict validation | 通过 |

以上执行均无失败或跳过；本轮 stock 只重跑相关测试，不声称重新运行完整 stock 矩阵。stock 实际解析为 UGUI 1.0.0 BuiltIn，VertexHelper SHA-256 为 `1623D631B78018C8171C9B13CD5372FDBC0CA53AAC4A36A65B10027A0DF37DD1`，无定制 CopyIndices。

新增 UT_UI_007..011 验证两种存储、固定/变化文本、增长/缩短/空输入、当前源位置/UV/颜色/索引、不可见字符、嵌套调用和异常恢复。UT_PERF_023 保留已知分配正控、字节可用性与饱和拒绝，并要求采样实际更新 holder 后 HUD 范围为零事件。

| 正向校准后的 128 次调用 | 原实现事件数 | 复用实现事件数 |
|---|---:|---:|
| 固定几何 Fill / Managed | 128 | 0 |
| 变化几何 Fill / Managed | 128 | 0 |
| 固定几何 Fill / Slim | 128 | 0 |
| 变化几何 Fill / Slim | 128 | 0 |
| 固定/变化文本完整 DoGenerate，两种存储 | 未单独测量 | 均为 0 |

计数使用当前线程 GC.Alloc 独立事件、已知 4096 B 分配正控和饱和检查，不把 marker duration 当作字节。上述结论仅覆盖已预热、固定输出长度/足够容量的调用。没有人工触发退出或程序集重载，也没有用私有字段验证生命周期钩子；这些路径经过代码审查，显式释放、嵌套释放和异常恢复有功能测试。

## 设备实验与来源

设备为 Mi 10（`791d02af`），原测试应用已先备份并原样重装，ADB 返回 Success，APK 读回 SHA-256 一致。正式实验使用既有 Sprite3/TMP6、三纹理、256 holders、Slim、Rotating、query reuse、60 FPS、60 预热/模式、3000 样本/模式，细分阶段诊断关闭。Development 分配诊断另用 300 样本/模式；两帧 drain 不计入普通模式分布。

Release 按 ABBA 顺序：01 基线、02 候选、03 候选、04 基线，中间存在下述暂停；05/06 为历史 null-Sprite 的单组基线/候选控制。随后三轮 Development：混合基线、混合候选、历史候选。历史控制不是重复 ABBA，不能单凭其差值归因。

首个采样会话在 02 候选轮被验收发现方向漂移：01 基线测后为 `1 / 2340×1080`，02 候选为 `0 / 1080×2340`。整组拒绝，03 中断，设备原应用/请求/九项设置已恢复读回；原始证据保留在本地 `invalid-orientation-session/`，不进入正式统计。修订协议 `per-launch-landscape-v2` 在每轮安装后、启动前重新设定方向，保存设置和真实 display 证据，启动后与采样后强验横屏；采样中不纠正方向，失败即拒绝整轮。新旧 APK manifest 均为相同的 fullUser 方向配置，无需改 APK。前轮绝对耗时也不与本轮直接比较收益。

两个 APK 的分阶段短测均复现：安装后 `(accelerometer_rotation,user_rotation)=(0,0)`；随后设置 `(0,1)`，启动两秒及报告完成后均为真实横屏 `1 / 2340×1080`，各完成 96 帧。短测后均恢复原 APK、请求不存在状态和九项设置，并由主代理独立核对。正式新会话在此验证后启动。

用户随后要求先确认以前的分辨率，并允许一致时保留。本轮通过 sentinel 暂停并恢复设备后，复核历史原始报告：2026-10-27 GPU 校准直接记录 Screen 为 `2340×1080`，2026-11-06 DAMAGE-03 正式报告记录 camera 像素为 `2340×1080`；前轮 HUD 则为同像素总量的竖屏 `1080×2340`，文档方向已更正，10/25 HUD 没有独立屏幕尺寸字段。依此保留原生横屏和修订协议已通过的首轮 A；被中断的 B 不计入统计，重新完整采集，继续剩余 B/B/A 和控制/诊断。暂停复核的时间间隔如实保留，不称为无中断采样。

原始 APK 保持不变：Release SHA-256 `E924501C42C8F142E72276796148D6F7F3E88BFB8A7719F38A83DD0947A17CAF`，Development `BC87FDCF839B520622E9FC17A85BA57D9191EA46896571FBECCAEE4D8B07F970`。候选写入独立 `Builds/HudRc/TmpScratch/`。构建清单比较确认影响运行/构建输入的差异只有 `TMPMeshDataUtility.cs`；其余为测试与文档。Release 的 1172 个构建输入在构建完成后逐项读回匹配，清单 SHA-256 为 `0F31840898E28E1441BB6B78AA521FDDE0EBF228A48D95760A52E4D033F96D8C`。

候选 Release APK SHA-256 为 `296633A5B26E6C60D4AD0868A150234523400F0F7793DE01DFC36A0933E627B8`，Development 为 `6A84788F70A073D20A29E000F21763324188BB6A0C8CE1A814BDE3B75C425E92`。Development 清单与 Release 完全相同，1172 个输入也在构建后读回匹配；两次构建均成功退出。

候选 Release Build GUID 为 `a4ba359491f44e5c86ebd7597def3b93`，Development 为 `7fa1712142de48e3a230a51649ef9323`。全部九轮的 APK/请求/报告哈希、构建与资源来源均核对通过；同配置组的请求/逐帧调度、三模式最终几何一致，启动后与采样后均为真实横屏。display 证据反映设备方向/物理尺寸，HUD 报告没有独立 Unity 渲染尺寸字段，不能将其表述为本轮直接读取了 Screen.width/height。

两次构建结束时的来源清单均全量读回匹配；后续更新的是结论文档、README、changelog 和测试记录，不声称最终文档字节与构建时完全相同。运行代码、测试、资源、依赖与设置须保持冻结输入一致。

## Release 主对照结果（ms）

| 顺序 / APK | Partial update p95 | Burst 刷新 median | Burst 刷新 p95 | 两帧累计 p95 |
|---|---:|---:|---:|---:|
| 01 基线 | 5.2483 | 9.8159 | 10.7505 | 20.8543 |
| 02 候选 | 4.9925 | 9.20435 | 9.9433 | 19.0519 |
| 03 候选 | 4.9356 | 9.1948 | 9.7828 | 18.9670 |
| 04 基线 | 5.3362 | 9.82005 | 10.7651 | 20.4918 |

以两次独立运行统计量的均值比较，Partial update median `3.202625→3.021600 ms`（下降 5.65%）、p95 `5.292250→4.964050 ms`（下降 6.20%）；Burst 刷新 median `9.817975→9.199575 ms`（下降 6.30%）、p95 `10.757800→9.863050 ms`（下降 8.32%），两帧累计 p95 `20.673050→19.009450 ms`（下降 8.05%）。没有把相邻帧或请求窗口当作独立实验重复。

两对相反顺序比较中，Burst median 分别下降 6.23% / 6.37%，p95 下降 7.51% / 9.12%。基线两次 Burst median 为 9.8159/9.82005 ms，候选为 9.20435/9.1948 ms。变化集中于 generation（基线 median 8.67015/8.67855 ms，候选 8.05530/8.02090 ms），merge median 均约 1.15 ms。generation 包含状态准备/排队/生成等，不是纯 TMP extraction 的独立 CPU 计时。

起止电池温度 30.7–31.2°C，thermal status 0/override false 均为端点观测，不证明全程无降频或后台干扰。首轮 A 结束到新 B 开始间隔 556.415115 秒；只有每版本两次独立运行且存在暂停，不做显著性、总体置信度或无条件因果收益声明。以上是本轮有界负载下的观测下降，完整帧、GPU 或业务替换收益仍未验证。

## 历史控制与 Development 分配诊断

历史 null-Sprite 单对 Release：Partial median `0.8510→0.84175 ms`，Burst 刷新 median `1.8395→1.8553 ms`、p95 `2.0065→2.0609 ms`，两帧累计 p95 `4.0162→4.0918 ms`。没有一致下降，符合本次改动针对 TMP 提取的范围；这只有一次顺序控制，不证明无回退，也没有复现旧的 1.5 ms 局部目标。前轮 HUD 为竖屏，不能将与其数值差异直接归因为实现变化。

| Development 诊断 | 样本帧（含排空） | HUD 范围分配事件 | 4096 B 正控事件 | 字节可用 |
|---|---:|---:|---:|---|
| 混合负载基线 | 906 | 10,360 | 1 | 否 |
| 混合负载候选 | 906 | 0 | 1 | 否 |
| 历史 null-Sprite 候选 | 906 | 0 | 1 | 否 |

基线 Partial 为 7,800 次、Burst 为 2,560 次，每帧事件数等于实际 completed holders；候选两轮所有采样帧均为零事件。三轮计数均可用且未饱和，正控有效。marker 单位均为 `TimeNanoseconds`，`bytesAvailable=false`、字节值为 `-1`，原始 marker 数值不是分配字节。因此结论是预热后、当前线程、有界 HUD 工作范围的分配事件消除，不是设备 0 B 或全应用零 GC。Development 耗时含诊断开销，不用于 Release 收益比较。

最终严格分析为 `complete=true`，九报告共 **56,754 帧**（六轮 Release 各 9,006 帧，三轮 Development 各 906 帧）。九轮电池温度端点范围 30.7–32.0°C；thermal status 为 0、override false，但 cooling device 存在非零状态，不能声明全程无降频。

测量结束后原应用恢复读回 SHA-256 为 `73C94BEAC9721C4685C6C0AFE0EBEB49528114B7379A2DFCDF8400CB261B2D22`，与备份一致；九项设置逐项相同，启动请求恢复为原来的不存在状态，原文件名全部保留，应用已停止。恢复记录 UTC 为 `2026-11-07T21:25:56.3267229Z`（北京时间 11 月 8 日）。

## 验收边界与本地证据

不调整预算，不关闭 PERF-04，不声明全应用零 GC、完整帧收益或 RC/发布通过。设备 scoped 字节、stock 场景性能、原性能预算、精确 RC/覆盖率门禁仍由 PERF-04 承接。Jenkins 不在本轮范围。

原始 XML、日志、构建来源、APK、报告和分析脚本仅保留本地 `TestResults/TmpScratch/`、`Builds/HudRc/TmpScratch/`；安装预检证据在 `TestResults/DeviceInstallCheck/2026-11-08/`，前轮原始证据保持在 `TestResults/Perf04/`。`analysis.json` 为默认严格分析，`root-acceptance.json` 为主代理独立核对，`restoration.json` 为恢复读回，`final-source-readback.json` 记录构建后的文档差异，`evidence.sha256` 绑定本轮原始证据、被拒绝组、短测和脚本。旧轮 190 个证据文件逐项重算，均未改变。

活动 TODO 已整体复核：PERF-04、GOV-002、MAINT-01 继续 M5；PERF-02、PERF-03、JOB-01 继续 M3；TMP-01..03 继续 M4。九项全部 carried-forward，GOV-002 仍 blocked/unverified，JOB-01 按预算触发，没有关闭或复用 ID。下一步优先量化代表性负载剩余 generation 成本，补 stock 场景性能、scoped 字节和精确 RC/覆盖率门禁；不将本轮子项完成扩为节点或发布通过。
