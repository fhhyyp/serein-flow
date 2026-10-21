# 源生成器框架交付与验证

验证日期：2026-10-21。实现位于 `src/ThirdParty/Serein.Generator.Framework/`。
两个生成器通过声明式管线共享特性发现、结构规则、分析状态、诊断路由、去重、输出和 Analyzer 交付能力，
保留独立的领域分析与代码模板。基础框架阶段已本地提交为 `6c60ed4`；声明式管线阶段保留在工作区待审阅。

## 已完成的验证

| 检查 | 结果 |
| --- | --- |
| Serein.Generator.Framework.Tests | 24/24 通过 |
| ScriptLang.Generator.Tests | 16/16 通过 |
| SereinFlow.Mcp.Generator.Tests | 25/25 通过，原有 22 项测试源码未修改 |
| SereinFlow.ScriptAdapter.Tests | 36/36 通过 |
| SereinFlow.Mcp.Tests | 106/106 通过 |
| SereinFlow.Mcp.Framework.Tests | 基础框架阶段 28/28 通过 |
| SereinFlow.ArchitectureTests | 基础框架阶段 36/36 通过 |
| 两个生成器同时通过 ProjectReference 使用 | 编译及实际脚本原型、MCP 调用通过 |
| 两个生成器同时通过本地 NuGet 使用 | 编译及实际调用通过，无框架运行时依赖 |
| 新生成器通过框架 NuGet 开发，再打包给独立项目消费 | 编译及执行通过，框架构建支持自动生效 |
| git diff --check | 通过 |

生成器测试覆盖声明注入、直接调用、派生工具特性、参数绑定、请求类型去重、跨文件 DTO 编辑、
无关编辑的增量输出复用，以及 ScriptLang 的命名空间、partial、同名类型、CLR 重载和多个脚本别名。
框架测试还验证取消传播、异常诊断、诊断位置、值相等集合、精确常量输出及嵌套泛型特性的元数据名称。
声明式管线新增验证覆盖 Skip/Success/Failure 路由、错误阻止模型工厂、警告继续输出、
精确与派生特性匹配、partial 目标归属、规则分支隔离、冲突目标排除、空集合输出、
规则/分析/输出异常、增量模型复用，以及分析结果创建后补报诊断的处理。
ScriptLang 新增方法体编辑后分析模型复用的检查。

执行独立交付验证：

```powershell
pwsh -NoProfile -File eng/verify-generator-packages.ps1
```

脚本同时检查生成器包内 `analyzers/dotnet/cs/Serein.Generator.Framework.dll` 的存在，
以及消费应用的运行时程序集引用和输出中没有框架 DLL。
独立消费项目遮蔽仓库构建默认配置，仅使用本地包源；包缓存写入本次验证目录。
声明式阶段日志位于 `artifacts/generator-verification/`：
`declarative-framework-final.log`、`declarative-script-final.log`、`declarative-mcp-final.log`、
`declarative-mcp-runtime.log`、`declarative-script-runtime.log` 和 `declarative-packages-final.log`。
新生成器消费样例使用 `FrameworkGenerator` 和私有不可变模型，验证公开入口不要求领域模型公开。

## 全量回归边界

以下全量及对照结果来自基础框架阶段。声明式阶段重新执行上表的生成器测试、
脚本适配/MCP 业务测试及三条独立交付路径，未再次执行全量解决方案测试。

未修改原有测试源码来绕过失败。
原始全量运行出现 Windows Event Log 写权限导致的三个 API 启动断言失败，
以及缺少 API key 时的 stdio 子进程退出超时。
这四项均在任务开始前 HEAD 的独立源码副本中复现；对照日志为
`artifacts/generator-verification/baseline-api-recheck.log`。

后续回归仅在验证进程中设置 `Logging__EventLog__LogLevel__Default=None`，
并过滤 `StdioModeFailsBeforeServingWhenTheExplicitKeyIsMissing`：

```powershell
$env:Logging__EventLog__LogLevel__Default = 'None'
dotnet test SereinFlow.sln --no-build --no-restore -m:1 `
  --filter 'FullyQualifiedName!~StdioModeFailsBeforeServingWhenTheExplicitKeyIsMissing'
```

该轮共执行 616 项，615 项通过；剩余一项为 Worker 的 EventBus 用例在收到后继节点完成事件之后，
取消并等待 Runner 退出时超时，位置为 WorkerSupervisorTests.cs 第 1059 行。
该用例与 Queue 用例一起单独重跑曾 2/2 通过。
进一步重复整套 Worker 测试，当前代码和任务开始前 HEAD 均出现过 86/86 通过，以及 85/86、在同一位置超时的结果，
确认该间歇问题在改造前也存在；不能宣称全量回归全绿。
相关日志为 `artifacts/generator-verification/clean-regression.log`、`worker-recheck.log`，
以及同目录的 `worker-repeat-1.log`、`worker-repeat-2.log`、`baseline-worker-repeat-1.log`、`baseline-worker-repeat-2.log`。
除排除的 stdio 用例外，API 集成测试 38/38 通过。

对照源码副本放在 `artifacts/generator-verification/obj/baseline/`，
避免其项目文件被仓库的架构检查计入正式源码。

## 仍保留的限制

- ScriptLang 现有 public NamingFormat 的重复声明可能产生 CS0436 警告；保留原公开声明的兼容性。
- 原型生成目前要求顶层、非泛型的 partial class；不支持的声明会报告 PROTOTYPE002。
- 重复的脚本成员名称改为 PROTOTYPE003 编译诊断；重载或多个别名应声明不同的 Name。
- 本轮不提供生成器性能基准；增量测试证明的是结果复用和正确失效。
- 基础框架阶段的仓库代码约定检查未通过：检查时 C# 报告 730 处，包含既有代码以及新增测试中的嵌套调用等写法。
  前端检查为 116 文件、0 违规。未修改规则、例外清单或原有测试来规避检查。

基础框架阶段已提交 `6c60ed4`；声明式阶段尚未提交。两个阶段均未推送 Git。
