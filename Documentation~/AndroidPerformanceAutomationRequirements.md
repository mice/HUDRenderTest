# Android 性能分析自动化需求分析

> 文档版本：1.0
>
> 日期：2026-08-07
>
> 适用工程：HUDRenderTest
>
> 状态：需求分析与方案基线，未包含实现代码
>
> 归属：仓库级自动化需求；不属于可分发 package Runtime，也不属于 Sample 运行时内容

## 1. 文档目的

本文档定义 Android 手机性能分析自动化系统的目标、边界、技术栈、控制协议、数据产物和 AI agent 监控方式。

核心约束是：

- `pytest` 负责测试流程、设备操作和确定性的通过/失败判定。
- AI agent 负责观察整个运行过程、关联证据、发现异常和生成诊断结论。
- AI agent 不直接覆盖 pytest 的结果，也不执行未授权的任意设备命令。

## 2. 现状盘点

Unity 工程已有一部分性能采样能力：

| 现有能力 | 位置 | 当前用途 |
|---|---|---|
| `PerfProbe` | `com.uidatarender/Runtime/UIDataRender/Diagnostics/PerfProbe.cs` | 记录 `fill_ms`、`draw_calls`，导出 CSV |
| 基线采样 | `HUDRenderTestSample/Assets/Scripts/HOT/PerfBaselineRunner.cs` | warmup、采样帧数、自动 Flush |
| 3-slot/8-slot A/B | `HUDRenderTestSample/Assets/Scripts/HOT/EightSlotPerfRunner.cs` | 两阶段采样并导出两份 CSV |
| 动态换图 | `HUDRenderTestSample/Assets/Scripts/HOT/DynamicSwapRunner.cs` | 重复换图、采样和 Flush |
| 批量场景脚手架 | `HUDRenderTestSample/Assets/Scripts/HOT/PrefabBatchSceneRunner.cs` | holder 数量、Slim、Job、纹理槽参数 |
| 场景控制器 | `HUDRenderTestSample/Assets/Scripts/HOT/BatchMergeBatcherRender.cs` | 重建、换图、Flush 和状态文本 |
| Unity 本地验证 | `Tools/Invoke-LocalValidation.ps1` | EditMode/PlayMode，不覆盖 Android Player |

仓库中目前未发现 pytest、ADB 设备适配层、Android Player 控制协议或 AI agent 事件入口。

## 3. 目标与非目标

### 3.1 目标

1. 用一个 YAML 文件描述设备、场景、参数、采样窗口和阈值。
2. 用 pytest 完成 APK 安装、启动、场景控制、采样、拉取和验收。
3. 统一采集 Unity 应用指标、Android 系统指标、日志和截图。
4. 每次运行生成可追溯、可复现、可离线分析的 artifact 包。
5. AI agent 在测试执行期间持续观察，并在异常时给出基于证据的诊断。
6. 支持单设备 MVP，后续扩展到设备池和 CI。

### 3.2 非目标

- 第一阶段不让 LLM 直接控制测试结果。
- 第一阶段不承诺跨厂商 GPU 利用率指标完全一致。
- 第一阶段不通过截图推算 FPS。
- 第一阶段不把 Unity Editor PlayMode 测试替换为 Android 测试；两者属于不同验证层级。

## 4. 总体架构

```text
┌──────────────────────────────────────────────────────────────┐
│                         pytest 控制平面                       │
│ 配置解析 · 设备锁 · APK 生命周期 · 场景状态机 · 阈值判定      │
└──────────────┬───────────────────────────────┬───────────────┘
               │ ADB                           │ 事件 JSONL/HTTP
               ▼                               ▼
┌──────────────────────────┐       ┌──────────────────────────┐
│ Android 设备与 Unity App  │       │ AI Agent Observer         │
│ 命令文件/广播 · PerfProbe │       │ 状态跟踪 · 异常关联       │
│ logcat · gfxinfo · dump   │       │ 证据摘要 · 诊断报告       │
└──────────────┬───────────┘       └──────────────┬───────────┘
               │ adb pull                         │ 可选补采集请求
               ▼                                  ▼
┌──────────────────────────────────────────────────────────────┐
│                       统一 artifact 目录                      │
│ run.json · events.jsonl · CSV · logcat · dumpsys · PNG · trace │
└──────────────────────────────────────────────────────────────┘
```

### 4.1 职责边界

| 组件 | 必须负责 | 不应负责 |
|---|---|---|
| pytest | 流程编排、超时、重试、阈值和最终结果 | 让 LLM 决定 pass/fail |
| ADB adapter | 执行经过校验的设备命令、捕获 stdout/stderr | 解析业务指标 |
| Unity control | 执行指定场景和参数、发出生命周期事件 | 了解 pytest 内部实现 |
| Collector | 获取和解析原始数据 | 修改测试状态 |
| AI agent | 观察、关联、解释、建议 | 任意 shell、绕过门禁 |
| Artifact store | 保存原始证据和索引 | 覆盖已有运行结果 |

## 5. 技术栈

### 5.1 Python 依赖

建议使用 Python 3.11 或更高版本，并固定依赖下限：

| 组件 | 版本 | 用途 |
|---|---:|---|
| `pytest` | `>=8.0.0` | 测试生命周期、fixture、参数化、JUnit 输出 |
| `PyYAML` | `>=6.0.0` | 读取和展开测试配置 |
| `requests` | `>=2.31.0` | 向 agent gateway 发送事件和诊断请求 |
| `numpy` | `>=1.24.0` | p50/p95/p99、离群值和重复运行统计 |
| `opencv-python` | `>=4.8.0` | 截图 ROI、SSIM/像素差和黑屏检测 |
| Python 标准库 | - | `subprocess`、`csv`、`json`、`pathlib`、`threading`、`statistics`、`uuid` |

不建议为了 MVP 引入 pandas、重量级消息队列或常驻数据库。原始数据使用 JSONL/CSV，汇总结果使用 JSON。

### 5.2 外部前置条件

- Android SDK Platform-Tools，提供 `adb`。
- Unity `2022.3.53f1c1` 和对应 Android Build Support。
- JDK/Gradle，版本以 Unity 2022.3 官方兼容范围为准。
- Android 手机开启 USB debugging，并已授权测试主机。
- CI 场景需要 self-hosted runner、稳定 USB Hub 和物理设备独占锁。
- 可选：Perfetto/Android platform trace 工具。

### 5.3 Prompt/Agent 技术栈

Prompt 层应采用供应商无关的 OpenAI-compatible HTTP 抽象，避免测试代码绑定某个模型厂商：

- `requests.Session`：HTTP 连接复用、超时、重试。
- `system prompt`：定义 agent 身份、权限和输出约束。
- `developer prompt`：定义事件格式、指标语义和证据规则。
- `user/context prompt`：注入本次运行的配置、快照和 artifact 索引。
- JSON 结构化输出：用于程序消费，不直接解析自然语言。
- 本地脱敏和上下文裁剪：限制日志长度、去除 token/路径中的敏感信息。
- Prompt 模板版本化：随报告记录 `prompt_id`、`prompt_version` 和模型标识。

模型不是硬性依赖。Agent 服务不可用时，pytest 必须继续运行并把事件写入本地队列。

## 6. pytest 执行流程

```text
PRECHECK
  -> DEVICE_LOCK
  -> DEVICE_READY
  -> INSTALL_APP
  -> LAUNCH_APP
  -> WAIT_READY
  -> APPLY_CASE_CONFIG
  -> WARMUP
  -> SAMPLE + SYSTEM_COLLECTION
  -> FLUSH_APP_DATA
  -> PULL_ARTIFACTS
  -> PARSE_AND_ASSERT
  -> REPORT
  -> CLEANUP
```

每个阶段都必须有：

- 明确开始和结束事件。
- 独立超时。
- 原始命令、退出码和 stderr。
- 可定位的失败原因。
- 最终清理动作。

设备断连、App 崩溃、ANR 或 Unity 无心跳时，测试必须在超时后失败，不能无限等待。

## 7. YAML 配置基线

```yaml
run:
  name: ui_perf_android
  apk: output/HUDRenderTest.apk
  package: com.example.hudrendertest
  activity: com.unity3d.player.UnityPlayerActivity
  repetitions: 5
  warmup_frames: 120
  sample_frames: 600
  timeout_seconds: 180

device:
  serial: auto
  min_battery_percent: 40
  max_temperature_c: 42
  orientation: landscape

cases:
  - id: meshdatax_n100
    scene: Scene_Perf_03_UIMeshDataX_Baseline
    parameters:
      count: 100
      use_slim: true
      continuous_rebuild: true

thresholds:
  app:
    fill_ms_p95: 1.0
  frame:
    fps_p05: 55
    jank_percent: 5
  memory:
    pss_mb: 300
  visual:
    ssim: 0.98

collect:
  logcat: true
  gfxinfo: true
  meminfo: true
  cpu: true
  thermal: true
  screenshots: true
  perfetto_on_anomaly: true
```

阈值必须按设备 profile 管理；示例数字不是所有 Android 机型的通用标准。

## 8. Unity 控制协议

现有 runner 主要通过 Inspector 按钮调用。Android 自动化需要增加一个稳定的 `AndroidPerfControl` 组件或等价控制层，统一封装：

- `start`：选择场景并设置参数。
- `stop`：停止当前采样。
- `flush`：落盘并报告 artifact。
- `reset`：清理 holder、材质和运行时状态。
- `heartbeat`：周期性报告帧号和当前阶段。

推荐 MVP 使用命令文件和事件 JSONL：

```json
{
  "schema_version": "1.0",
  "run_id": "20260807_001",
  "case_id": "meshdatax_n100",
  "action": "start",
  "parameters": {
    "count": 100,
    "use_slim": true,
    "warmup_frames": 120,
    "sample_frames": 600
  }
}
```

事件最少包含：`ready`、`started`、`warmup_done`、`sample_started`、`csv_ready`、`heartbeat`、`completed`、`failed`。

### 8.1 现有 Unity 代码需要补齐的接口

- `PerfProbe` 导出每帧或分位数所需的原始样本，不仅是 avg/max。
- Flush 文件名包含 `run_id`，避免同秒覆盖。
- CSV context 增加 case、构建版本、渲染模式、holder 数、batch 数等信息。
- Player 将 CSV 写入可被 `adb pull` 的目录，并返回设备相对路径。
- 增加心跳和失败事件，不能只依赖 UI `statusText`。

## 9. 指标与采集要求

### 9.1 应用指标

- `fill_ms`、`merge_ms`、`draw_calls`。
- p50/p95/p99、最大值、样本数和有效帧数。
- holder 数、batch 数、纹理槽、Slim/Job/8-slot 配置。
- 启动耗时、重建耗时、GC Alloc、异常日志数。

### 9.2 Android 指标

- `dumpsys gfxinfo <package>`：帧统计、jank、渲染耗时。
- `dumpsys meminfo <package>`：PSS、Java/Native 内存。
- `/proc/<pid>/stat` 或 `dumpsys cpuinfo`：进程 CPU。
- `dumpsys thermalservice`、`dumpsys battery`：温度、热状态、电量。
- `logcat`：崩溃、ANR、Unity exception 和控制协议错误。
- Perfetto：异常时或专项性能分析时采集。

厂商 GPU 利用率/频率接口不统一，缺失时应标记为 `unavailable`，不能误判为零。

### 9.3 视觉指标

OpenCV 用于截图质量和渲染正确性：ROI 比对、SSIM、像素差、黑屏/空白检测。FPS 不从截图推算。

## 10. Artifact 目录规范

```text
artifacts/<run_id>/
  run.json
  config.resolved.yaml
  events.jsonl
  pytest.junit.xml
  summary.json
  logcat.txt
  dumpsys/
    gfxinfo.txt
    meminfo.txt
    cpuinfo.txt
    thermalservice.txt
  app/
    perf_*.csv
    raw_samples.jsonl
  screenshots/
    start.png
    end.png
  traces/
    anomaly.perfetto-trace
```

`run.json` 必须保存设备指纹、APK hash、Git commit、Unity 版本、配置 hash、开始/结束时间和 Prompt 版本。

## 11. Agent 事件协议

```json
{
  "schema_version": "1.0",
  "run_id": "20260807_001",
  "timestamp_utc": "2026-08-07T12:00:00Z",
  "device_serial": "ABC123",
  "phase": "sampling",
  "type": "metric_snapshot",
  "payload": {
    "fill_ms_p95": 1.24,
    "fps_p05": 52.1,
    "jank_percent": 8.4,
    "pss_mb": 218
  }
}
```

事件传输要求：

- 本地 `events.jsonl` 永久保底。
- HTTP 上报异步执行，不阻塞 pytest。
- 请求设置连接和读取超时，失败后指数退避。
- 每个事件带 `run_id` 和单调序列号，支持乱序检测和断点恢复。
- Agent 不可用时，报告标记 `observer_status: unavailable`，但保留测试结果。

## 12. Prompt 工程设计

### 12.1 Agent System Prompt

```text
你是 Android 性能自动化运行观察员，不是测试结果裁判。

你的任务：
1. 根据事件流维护当前 run、case 和 phase 状态。
2. 只使用提供的原始指标、日志和 artifact 作为证据。
3. 发现异常时，说明异常指标、时间窗口、对照组和证据文件。
4. 区分“已证实事实”“高概率推断”和“仍需采集的信息”。
5. 不得修改 pytest 的 pass/fail 结果。
6. 不得执行任意 shell、ADB 或网络操作。
7. 只有收到允许的 action schema 且动作在白名单内时，才可提出补充采集建议。
8. 输入缺失或指标不可用时，输出 unknown，不得编造数值。

输出必须是合法 JSON，不要输出 Markdown。
```

### 12.2 运行监控 Prompt

```text
请分析以下 Android 性能运行快照。

运行信息：
{run_metadata}

测试配置：
{resolved_config}

最近事件：
{recent_events}

指标快照：
{metric_snapshots}

日志摘要：
{log_summary}

请返回：
{
  "status": "healthy|warning|critical|unknown",
  "anomalies": [
    {
      "metric": "...",
      "observed": 0,
      "threshold": 0,
      "severity": "warning|critical",
      "evidence": ["artifacts/..."],
      "confidence": 0.0
    }
  ],
  "hypotheses": [
    {
      "cause": "...",
      "supporting_evidence": ["..."],
      "counter_evidence": ["..."],
      "confidence": 0.0
    }
  ],
  "recommended_next_collection": [
    "gfxinfo|meminfo|logcat|perfetto|screenshot|none"
  ],
  "operator_message": "..."
}
```

### 12.3 结束报告 Prompt

```text
请基于完整运行证据生成性能诊断摘要。

要求：
- 先列出确定性测试结果，再列出 AI 解释。
- 对重复运行报告 median、p95 和离散程度。
- 明确区分性能回归、设备热降频、应用异常、数据不足。
- 每个结论必须引用 artifact 路径或事件序号。
- 不要把相关性写成因果性。
- 如果无法定位根因，输出最小的下一步实验。

返回：
{
  "test_result": "passed|failed|blocked",
  "executive_summary": "...",
  "facts": [],
  "regressions": [],
  "likely_causes": [],
  "data_gaps": [],
  "next_experiments": [],
  "evidence": []
}
```

### 12.4 Prompt 安全与可观测性

- Prompt 模板放在版本库中，使用 `prompt_id` 和 `prompt_version`。
- 运行报告保存模型名称、请求时间、输入摘要 hash 和输出 JSON。
- 发送前过滤设备序列号、访问令牌、用户数据和绝对本机路径。
- 原始 logcat 不全部塞入 Prompt；先按时间窗、级别和关键字压缩。
- 每次模型结论都必须能回链到原始 artifact。
- 模型调用失败、超时或输出 JSON 不合法时，降级为 `unknown`，不能阻断 pytest。

## 13. 非功能需求

| 类别 | 要求 |
|---|---|
| 可复现性 | 固定设备、方向、刷新率、构建 hash、warmup 和 sample 配置 |
| 稳定性 | 每个阶段独立超时；设备断连后可诊断退出 |
| 统计 | 默认至少 3～5 次重复；报告 median、p95 和离散程度 |
| 热稳定 | 运行前检查温度，连续 case 之间设置冷却策略 |
| 并行 | 一台设备同一时间只运行一个 case；多设备按 serial 分片 |
| 可追溯 | 所有命令、事件、原始输出和模型版本可追溯 |
| 安全 | Agent 只能访问白名单 artifact 和白名单动作 |
| 离线 | 无网络时 pytest 可运行，Agent 事件本地留存 |

## 14. 验收标准

### MVP

- `pytest -m android` 能完成设备准备、APK 启动、场景执行、数据拉取和报告生成。
- 每次运行生成唯一 `run_id` 和完整 artifact 目录。
- CSV、logcat、gfxinfo、meminfo、截图都能关联到 case。
- pytest 通过/失败只由 YAML 阈值和确定性健康检查决定。
- Unity 无心跳、App 崩溃或设备断连会在超时后明确失败。
- Agent 事件上报不可用时，测试仍能完成。

### 第二阶段

- 异常时自动触发 Perfetto 或更高频系统采集。
- 支持多设备矩阵、基线比较和 CI 门禁。
- 支持受限的重启、补采集和冷却动作，并留下完整审计记录。

## 15. 风险与未决决策

| 问题 | 需要确认的决策 |
|---|---|
| APK 身份 | package name、activity、Development 还是 Release |
| 设备池 | 单台固定设备，还是多机型矩阵 |
| 控制通道 | MVP 命令文件，还是直接做 Android broadcast/HTTP |
| 基线 | 每种设备的阈值、重复次数和热降频处理 |
| 视觉验收 | 是否需要截图基线、ROI 和 SSIM 门槛 |
| Agent 权限 | 只读观察，还是允许白名单恢复动作 |
| 模型服务 | 本地模型、内网网关或云端 OpenAI-compatible API |

## 16. 推荐实施顺序

1. 建立 `automation/android_perf`、YAML schema、ADB adapter 和设备锁。
2. 增加 Unity `AndroidPerfControl`、心跳、命令和事件协议。
3. 接入 App CSV、logcat、gfxinfo、meminfo、thermal 采集。
4. 补充原始样本、p95 统计、唯一文件名和 Android artifact 导出。
5. 增加 OpenCV 截图检查和确定性阈值门禁。
6. 接入 JSONL/HTTP Agent Observer 与 Prompt 模板。
7. 最后加入 Perfetto、设备矩阵和受限自动恢复。

## 17. 交付物清单

- Python 虚拟环境和依赖锁定文件。
- pytest 测试入口及 fixtures。
- Android 设备适配和 ADB 命令审计日志。
- Unity Player 控制组件和协议说明。
- YAML 场景/阈值配置。
- 指标解析器、报告生成器和 artifact 目录。
- Agent gateway client、Prompt 模板和结构化输出 schema。
- 单设备本地运行说明及 CI 接入说明。
