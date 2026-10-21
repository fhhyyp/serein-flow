# 源生成器框架交付与验证

验证日期：2026-10-21。实现位于 `src/ThirdParty/Serein.Generator.Framework/`。
两个生成器保留独立的发现规则、领域模型及代码模板，共用输出、符号、特性、常量、诊断和 Analyzer 交付能力。

## 已完成的验证

| 检查 | 结果 |
| --- | --- |
| Serein.Generator.Framework.Tests | 9/9 通过 |
| ScriptLang.Generator.Tests | 15/15 通过 |
| SereinFlow.Mcp.Generator.Tests | 25/25 通过，原有 22 项测试源码未修改 |
| SereinFlow.ScriptAdapter.Tests | 36/36 通过 |
| SereinFlow.Mcp.Tests | 106/106 通过 |
| SereinFlow.Mcp.Framework.Tests | 28/28 通过 |
| SereinFlow.ArchitectureTests | 36/36 通过 |
| 两个生成器同时通过 ProjectReference 使用 | 编译及实际脚本原型、MCP 调用通过 |
| 两个生成器同时通过本地 NuGet 使用 | 编译及实际调用通过，无框架运行时依赖 |
| 新生成器通过框架 NuGet 开发，再打包给独立项目消费 | 编译及执行通过，框架构建支持自动生效 |
| git diff --check | 通过 |

生成器测试覆盖声明注入、直接调用、派生工具特性、参数绑定、请求类型去重、跨文件 DTO 编辑、
无关编辑的增量输出复用，以及 ScriptLang 的命名空间、partial、同名类型、CLR 重载和多个脚本别名。
框架测试还验证取消传播、异常诊断、诊断位置、值相等集合、精确常量输出及嵌套泛型特性的元数据名称。

执行独立交付验证：

```powershell
pwsh -NoProfile -File eng/verify-generator-packages.ps1
```

脚本同时检查生成器包内 `analyzers/dotnet/cs/Serein.Generator.Framework.dll` 的存在，
以及消费应用的运行时程序集引用和输出中没有框架 DLL。
独立消费项目遮蔽仓库构建默认配置，仅使用本地包源；包缓存写入本次验证目录。
最新交付检查日志：`artifacts/generator-verification/packages-verified.log`。

## 全量回归边界

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
- 仓库代码约定检查未通过：检查时 C# 报告 730 处，包含既有代码以及新增测试中的嵌套调用等写法。
  前端检查为 116 文件、0 违规。未修改规则、例外清单或原有测试来规避检查。

所有更改保留在工作区，未提交或推送 Git。
