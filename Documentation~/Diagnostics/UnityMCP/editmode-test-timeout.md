# Unity MCP EditMode 测试超时记录

## 时间

2026-09-27

## 项目环境

- Unity：2022.3.53f1
- 项目：`HUDRenderTestSample`
- Unity MCP / Agent Bridge：1.3.1
- 活动场景：`Assets/Scenes/Scene_Perf_HUD_RC.unity`
- 测试模式：EditMode

## 现象

Unity MCP 已成功连接，编辑器状态检查正常：

- `runtimeMode`: `edit_mode`
- `activityState`: `idle`
- `isCompiling`: `false`
- `isUpdating`: `false`
- `isPlaying`: `false`
- 场景已加载且未修改

通过 MCP 调用 EditMode 测试：

```text
unity_tests_run_edit_mode
```

请求等待约 120 秒后返回：

```text
success: false
status: timeout
code: MCP_TIMEOUT
Timed out waiting for result
```

随后再次读取编辑器状态，Unity 已恢复 `idle`，没有处于编译或运行状态。

## 已知结果

本次 MCP 调用没有返回测试汇总，也没有生成可供 MCP 读取的测试结果报告，因此不能仅凭该调用判断测试是否执行完成。

此前通过仓库本地验证脚本执行的相同 EditMode 测试结果为：

- 126/126 通过
- 包布局检查通过
- `MultiplyPoint3x4` 与 `MultiplyPoint` 等价性测试通过

## 控制台信息

MCP 读取到的错误和警告不是本次 mesh 变换修改引起的：

### 既有错误

Agent Bridge 包目录中的部分脚本缺少 `.meta` 文件，Unity 报告这些资源位于 immutable folder 并忽略：

```text
Packages/com.unitymcp.agent-bridge/Tests/Editor/AgentTaskEditor has no meta file
Packages/com.unitymcp.agent-bridge/Runtime/AgentTaskCore has no meta file
Packages/com.unitymcp.agent-bridge/Editor/AgentTaskEditor has no meta file
```

### 既有警告

- `TextureSlotTable` 超过纹理槽容量的测试警告
- `MergeBatcher` 拆分 batch 的测试警告
- UGUI vertex buffer layout 自动调整警告
- MCP plugin abstractions 的 Documentation~ 目录 meta 警告

未发现 `UIImage`、`UIMeshData`、`UIMeshDataX` 或 `MultiplyPoint3x4` 相关错误。

## 相关命令与标识

- 编辑器状态命令：`unity_editor_get_state`
- EditMode 测试命令：`unity_tests_run_edit_mode`
- 测试命令 ID：`20260927_145900520_03176_000003`
- 内部等待结果 ID：`20260927_145900522_03176_000004`
- 后续等待仍返回超时

## 后续排查建议

1. 检查 Agent Bridge 对 `unity_tests_run_edit_mode` 的默认超时时间和测试结果轮询逻辑。
2. 使用单个测试或单个程序集测试，确认是否只有完整测试集超时。
3. 检查 `Temp/AgentBridge/outbox/` 是否产生了对应结果文件但未被桥接层读取。
4. 检查 Unity Test Runner 是否被已有测试运行状态、弹窗或日志处理阻塞。
5. 清理 Agent Bridge 包中缺少的 `.meta` 文件，避免导入错误干扰诊断。
6. 若 MCP 仍超时，继续使用仓库脚本：

```powershell
.\Tools\Invoke-LocalValidation.ps1 -SkipPlayMode
```

## 状态

待后续处理 Unity MCP 测试结果轮询或超时问题。
