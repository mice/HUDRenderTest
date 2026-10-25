# PERF-04 收尾与 M5 交接

## 已交付

专用场景、三类确定性负载、真实 holder/mesh/batch 渲染路径、预热/采样/导出边界、配置快照、Run/Reset 和全局设置恢复已实现。报告保存来源标识、逐帧数据、集中刷新子集及不可用计数器状态。

2026-10-20 最新本地验证为 EditMode 147/147、PlayMode 28/28、治理 175/175、包布局通过，见 [共同收尾记录](../../2026-10-21-optimize-holder-buildmesh-incremental/evidence/closure-validation.json)。本轮未执行覆盖率、clean consumer 或 Jenkins。

性能来源是基于 `3a6f553` 的 dirty 工作树，不是候选提交。已保存两组独立证据：

- [Managed/Slim × quad timestamps 开关的 8 轮对照](../../2026-10-20-profile-simple-quad-write/evidence/android-results.md)：14,400 帧，说明细分计时对写入成本存在明显扰动。
- [Slim fill 修复前后四轮对照](../../2026-10-21-optimize-slim-fill-color-check/evidence/android-results.md)：7,200 帧，APK、源码、配置及报告哈希完整；固定版 Partial regeneration median/p95 为 1.2090–1.2184 / 1.3013–1.3207 ms，Burst refresh 为 7.0359–7.0925 / 7.1391–7.2291 ms。

本轮又补充 Sprite 负载与强制 fallback 采样选项。旧报告仍描述其原始源码；不能把旧 fingerprint 冒充当前采样器或未来 RC 的 fingerprint。

## 未通过的条件

当前总体验收状态为 **`inconclusive/environment-limited`**，不是 passed，也不以环境缺口直接判定产品性能失败：

1. Release IL2CPP 的 scoped allocation 和 frame GC 计数器不可用。Editor 的 warm allocation 测试及 IL2CPP 装箱代码证明只支持各自范围，不能代替真机场景分配验收。
2. 现有场景测量仍超过历史 1 ms 参考，没有本轮用户确认的使用预算例外。由于环境门尚未完整通过，本轮不请求预算例外。
3. 原验收矩阵包含 64/256 holders、Managed/Slim。最新重复对照集中于 256 holders，不能宣称整个矩阵已完成。
4. 没有绑定精确候选提交的 clean validation、当前覆盖率或 Jenkins run；版本仍是 0.1.2 Unreleased。

## 可执行的后续及归属

- **PERF-04 / M5**：先建立不会触发已知 IL2CPP API 崩溃的、带正向校准的目标设备分配测量方式；环境门未通过前保留 inconclusive。
- 同一精确候选、固定诊断开关下完成 64/256 holders × Managed/Slim 的重复采样，保留 APK/source/config/report 哈希、温度及帧时间。
- 环境有效后对照使用预算。若需例外，提交实测负载、适用设备/更新频率、风险及重开条件，再取得明确确认；当前不放宽预算。
- **VERIFY-02**：解决 stock UGUI / 定制 CopyIndices 的独立消费依赖契约。
- **VERIFY-01 / GOV-004**：在候选提交完成覆盖率、clean install、完整回归及真实 Jenkins 验证。不得由本次归档自动推导 RC 或正式发布许可。

## 本 change 的处理边界

4 个优化 change 可在各自证据齐备后以完成状态同步归档。本 change 的 2.3、3.4 保持未勾选；上文是可审阅的交接材料，并不冒充性能通过或正式交接完成。

若用户选择将这轮场景工具/测量工作按阶段归档，归档必须明确为 **未验收的阶段交付**，保留原任务勾选和上述缺口，由活动 PERF-04 / M5 继续承接。否则保留本 change 活动，待完整环境与预算条件满足后再归档。


## 用户确认的后续方向

2026-10-20 用户确认“确实未通过”，要求新开 change 探索 256 个 holder 分为 2 个、每桶 128 个 mesh 的分桶策略。这是将未通过结果交给后续探索的决定，不是预算例外或 RC 接受。本 change 以未验收的阶段交付归档，保留 2.3/3.4 未勾选；PERF-04/M5 及后续分桶 change 继续承接，分配环境、候选验证等独立缺口不因分桶自动消失。
