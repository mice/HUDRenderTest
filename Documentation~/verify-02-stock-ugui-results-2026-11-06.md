# VERIFY-02：stock UGUI 独立分发验收

日期：2026-11-06。仓库：`D:/UnityPackages/HUDRenderPackage`；交付包：`com.uidatarender`。节点：M5；目标：`0.1.2` Unreleased。

## 结论

**VERIFY-02 completed。** 包内新增公共 API 索引提取回退，实际 stock UGUI 1.0.0 消费环境已编译并通过正确性、预热分配和完整回归。原 Sample 的定制 UGUI 快速路径也通过回归。主规格已同步；本次没有创建提交、RC、tag 或发布。

## 实现与边界

- `VertexHelperUtils.CopyIndices` 通过复用的 Mesh/List 调用 `FillMesh/GetTriangles`，保留原索引身份、顺序及目标/顶点偏移，使用减法检查目标容量，避免偏移相加溢出。
- 兼容的定制 UGUI 实例方法优先于扩展方法；原 Sample 继续直接复制索引。测试显式调用静态扩展入口，保证在两种依赖环境下都实际覆盖回退。
- `ReleaseIndexScratch` 支持显式释放；编辑器重载/退出、运行时子系统重置/应用退出也清理资源。Play Mode 使用延迟销毁。提取与清理仅支持 Unity 主线程。
- stock 非空索引提取继承 `FillMesh` 的 **小于 65,000 顶点**限制；测试确认 64,999 顶点可提取、65,000 顶点显式抛异常且不写目标，并验证异常后的恢复。scratch 保留最大已用容量，扩容和释放后首次使用允许分配。
- 新增 `UT_MESH_031..036`，覆盖重复顶点属性但不同索引身份、未引用顶点、流缩短、偏移、非法范围、空流、预热分配、原生资源释放/重建及顶点上限。

## 实际验证

编辑器：Unity **2022.3.53f1**。用户明确授权自由使用编辑器后执行；本会话未提供 Unity MCP 工具，使用仓库脚本。PlayMode 采用实际 **D3D11** 图形设备。

| 环境 | 提取专项 | 完整 EditMode | 完整图形 PlayMode |
|---|---:|---:|---:|
| 原 Sample / 定制 UGUI | 13/13 | 196/196 | 61/61 |
| stock UGUI 消费快照 | 13/13 | 196/196 | 61/61 |

表中最终运行均无失败、无跳过。专项与完整测试有包含关系，不累加为独立用例数。治理检查 **257 tests / 257 records**；原仓库及 stock 快照布局通过；OpenSpec strict 校验 **20/20**（19 份主规格、1 个 change），diff 检查通过。

`UT_MESH_017` 在两种环境下均记录 10,000 次预热后提取的 `FillData2AllocationEvents=0`、`FillData3AllocationEvents=0`；`UT_MESH_034` 强制回退的 10,000 次调用也断言为 0。沿用有显式正向分配校准的 `GC.Alloc` 计数器。该结论限定被测提取作用域，不代表整个应用零 GC。

## 依赖与源码来源

原 Sample 继续使用 `file:../../com.unity.ugui`，保留用户对 agent-bridge manifest/lock 的修改。stock 环境只在临时消费副本中固定 UGUI 1.0.0，并移除可选 agent 工具依赖；包声明的 Burst 1.8.28、TMP 3.0.6、UGUI 1.0.0 未修改。

| 项目 | 定制 UGUI | stock UGUI |
|---|---|---|
| 来源 | 仓库内本地包 | 实际解析的 BuiltIn 1.0.0，lock 为 builtin |
| 源码位置 | `com.unity.ugui/Runtime/UI/Core/Utility/VertexHelper.cs` | 验证项目 `Library/PackageCache/com.unity.ugui@1.0.0/Runtime/UI/Core/Utility/VertexHelper.cs` |
| 实例 `CopyIndices` | 存在 | 不存在，经编译后反射核验 |
| `VertexHelper.cs` SHA-256 | `40EE6F876DEEB7BF8E4084B5B1920E087914D3C1F6E308A7D8759951A5479AE9` | `1623D631B78018C8171C9B13CD5372FDBC0CA53AAC4A36A65B10027A0DF37DD1` |

stock 哈希与此机器 Unity 安装目录自带的源码一致。实际解析路径、版本、来源和哈希由编译后编辑器探针采集，runner 同时检查实际 lock 和定制方法缺失，未以预写 manifest/lock 作为编译证明。

工作树基准 HEAD 为 `520b4c16c2de0ea2741a66c5e94c1b61d5fa461d`，测试包含未提交改动。初次无 Library 快照的导出 tree 为 `4bfe75e59d9187d8c6dfd80250efecad21af323e`；最终完整 stock 回归导出 tree 为 `411c67354b37c5ac27be87e357773f1237bb70d7`。这些 tree 标识导出的源内容，消费 manifest 转换和探针注入另由脚本记录；不是发布提交。

被测兼容源码 SHA-256：原文件 `3DF52206FB5D0100055ADFD9014E916323AE9E751AD39E6BB0E057E7FB82D661`，stock 副本 `8D8476BF993BB435C2507A76E6D6142084A77548B74BE7B2F80C982B1CF37796`。提取测试原文件为 `D6858DBC7E420BAE245091D4EDA472B335592B9FB16BD1BB30A205F0DA127396`，stock 副本为 `F202A6150F333F77454AF55C7947D203D4B17C3484ABC0CF761594F028DBC32C`。两组差异均仅为 Git 导出的换行转换，标准化换行后逐字符一致。之后的收口只更新文档和测试记录，未改变被测 Runtime、Shader 或测试定义。

## 快照与失败审计

仅使用 `D:/workspace/HUDRenderPackageValidation` 这一份可复用验证项目。首次导入日志确认缺少 Library 并重新创建；后续回归保留 Library，源码仍不包含定制 UGUI。没有复制原 Sample 的 Library 或修改 Unity 安装包。

首次 stock 完整图形 PlayMode 为 **60/61**：`UT_DAMAGE_106` 的 `traceabilityComplete` 断言失败。原 clean pipeline 的普通文件导出移到 `D:/workspace/` 后，Sample 无法读取 Git 提交和状态。运行时提取及该测试的回放/图形断言未报告失败。

已将同一个自有快照原地转换为基于原 HEAD 的 **detached worktree**，保留 Library，覆盖当前可跟踪源文件；报告读取真实 HEAD、dirty 状态和实际源码指纹。来源追踪专项随后 **1/1** 通过，最终在同一 worktree 重跑完整 **196/196 + 61/61**。断言保持原样，初次失败 XML/log 单独保留；未修改共享历史、分支或仓库配置。

## 复现与本地证据

```powershell
.\Tools\Invoke-LocalValidation.ps1 -SkipPlayMode -TestFilter TestVertexHelperUtils
.\Tools\Invoke-CleanValidation.ps1 -TestFilter TestVertexHelperUtils
.\Tools\Invoke-LocalValidation.ps1 -WithGraphics
.\Tools\Invoke-CleanValidation.ps1 -TestPlatform Both -WithGraphics
```

原始 XML、日志、源清单和哈希留在本地：

- `TestResults/Verify02/`：原 Sample 专项/完整结果；stock 首次冷导入专项与 source manifest；初次 PlayMode 失败和来源追踪复测；源码等价检查；OpenSpec 结果。
- `TestResults/CleanValidation/`：最终 stock 完整 XML/log、编译后 UGUI 来源证明、实际解析 lock、包文件 SHA-256 清单与布局/治理报告。`source-snapshot.json` 的依赖未验证标记描述准备时状态；运行后的来源证明和 NUnit 结果用于实际验收。
- `HUDRenderTestSample/Logs/`：原 Sample runner 输出。上述原始工件不纳入提交。

## TODO 出口与后续

完整复核 10 项活动 TODO，`VERIFY-02 / M5 / v0.1.2` 按上述证据归档为 completed，并从活动队列移除。其余 **9 项**延续：M5 的 PERF-04、GOV-002、MAINT-01；M3 的 PERF-02/03、JOB-01；M4 的 TMP-01..03。GOV-002 继续 blocked/unverified，JOB-01 仍按预算触发；无 ID 复用。

下一步为 PERF-04 的代表性负载、设备分配和候选来源验证，并记录所用 UGUI 路径。stock 回退包含额外 Mesh 往返，本次功能/分配回归不构成性能预算接受；原 PERF-01 与 DAMAGE-02/03 历史证据保持各自范围。覆盖率、Android/IL2CPP 和 RC/发布门禁未在本次重跑，90.0% 覆盖率仍是历史值。
