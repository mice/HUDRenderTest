# HUD RC 专用场景配置与入场快照

2026-09-27：全量审阅 Documentation~/todo.md、PERF-01 归档和当前 proposal/design/spec/tasks。后续 ID 已存在，沿用 PERF-04（M5 / v0.1.2）；GOV-002、GOV-004 及其他 M3/M4 条目继续留在活动队列，不触发发布。

## 可重放配置

- 场景：HUDRenderTestSample/Assets/Scenes/Scene_Perf_HUD_RC.unity。
- 默认 256 holders，每个 3 个简单 UIImage quad（4 顶点、6 索引），总计 3072 顶点、4608 索引。两条配置路径：UIMeshData / UIMeshDataX，使用真实 BuildMesh/FillVertex、HolderBatchRenderer 和 DrawMesh。
- seed 12345，固定网格摆放，宽度按索引/帧号确定变化。使用无 sprite 的 UIImage（实心色条），不把它描述为文本/TMP 或复杂混合 HUD 的全覆盖。
- Static：采样帧不标脏，不重新生成，保持实际绘制。
- Partial：每帧按固定轮转顺序更新 ceil(10% × holderCount) 个 holder。
- Burst：每 30 个采样帧整批更新；更新帧独立汇总。
- 每模式 60 帧全量生成预热，再采样 180 帧；每个报告 540 个样本，含 6 个 burst 刷新样本。扩展测量可提高采样帧数，配置随报告保存。
- vSync=0、targetFrameRate=-1、timeScale=1，结束或 Reset 恢复原设置。该配置测吞吐，不伪装为锁定 60 FPS 的设备保证。

## 测量口径

Stopwatch 测 generation（UIImage.DoGenerate、FillVertex 和变换）、merge（含生产批次分配）、draw submission。GC.GetAllocatedBytesForCurrentThread 测上述边界；generation 不是 extraction-only，因此微基准 0 B 不被扩大到整个场景。ProfilerRecorder 的 Main Thread 与 GC Allocated In Frame 读上一帧并与 pending sample 对齐；缺失时标为 unavailable/-1。Update-to-Update 间隔含编辑器及等待成本，不与 regeneration CPU 混用。

采样数据预分配，采样内不写文件或日志；结束后 JSON 保存环境/配置/逐帧数据/统计，CSV 保存样本。统计使用 nearest-rank median/p95，burst 更新帧单列。实际场景计数器不可用时记录验收缺口，不填伪零。

## 接线与资源

使用无活动 Canvas 渲染的共享 UIImage 生成模板，直接调用公开 holder.BuildMesh，再进入真实批渲染器；不将临时 owner 注册到无注销接口的全局 OwnerRegistry。结束释放模板、每个 IUIData、合批 Mesh/Material 和 recorder。场景持久 Camera/Runner 保留供重跑。

## 测试分配

UT_PERF_012：提交场景引用和真实几何；013：三模式工作量和双路径重放；014：中途 Reset/完成时设置恢复与对象清理；015：配置快照、报告回读和 burst 子集。无噪声敏感的毫秒断言。

本记录为配置与入场设计，实际测量和验收另记；旧 1 ms p95 不是自动通过门槛。
