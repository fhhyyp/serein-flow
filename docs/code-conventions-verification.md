# 全仓规范重构验证记录

验证日期：2026-10-20。所有修改保留在本地，未提交或推送。

## 覆盖与工具

检查入口为 `eng/check-code-conventions.ps1 -SelfTest`。前端使用 TypeScript、Vue 编译器和 PostCSS；C# 使用 SDK 自带的 Roslyn。检查覆盖自有生产代码、测试、示例、工具和文档站，通过 Git 枚举文件，排除第三方源码、依赖和构建产物。

最终扫描前端 116 个文件，包括 38 个 Vue、73 个 TS/模块配置、3 个 CSS 和 2 个检查器 TS 文件；C# 293 个文件，包括原有 292 个文件和检查器。两者均为零违规。前端检查器 13 个回归用例及 C# 检查器自测通过，包含回调边界、可选链、构造调用及构造初始化器、await、隐式返回、解构、顶层与嵌套同行语句、跨行语句尾部、模板布局、动态指令参数、CSS 层级和宏例外。

使用 Node 24.19.0、npm 11.6.2、.NET SDK 10.0.401。主站新增显式编译器及 PostCSS 开发依赖并同步锁文件。主站 `npm ci --dry-run --ignore-scripts --offline` 通过；两站依赖均已安装。CI 增加独立的规范与前端检查作业，包含规范自测、主站测试和构建、文档站构建及 npm 版本固定，不依赖 .NET 解决方案作业成功。

## 构建与测试

| 检查 | 结果 |
| --- | --- |
| 全仓规范扫描及检查器自测 | 通过 |
| 主站 `npm test` | 88/88 通过 |
| 主站 `npm run build`，包含 vue-tsc | 通过 |
| 文档站 `npm run build` | 通过 |
| 34 个可用 .NET 项目 Release 构建 | 通过，零错误 |
| .NET 测试各项目最新有效结果 | 470 通过，5 失败，共 475 |
| C# 公共及受保护声明审计 | 292 个文件，零签名差异 |
| TS 导出显式声明审计 | 73 个文件，零差异 |
| PostCSS 样式语义树审计 | 41 个 Vue/CSS 文件，零规则、选择器、属性值或顺序变化 |
| Vue 静态文本审计 | 38 个文件，无非空白文本变化 |
| `git diff --check` | 通过 |

TS 声明审计忽略等价分隔符和数值书写差异，推断返回类型由类型检查及测试补充验证。Vue 标签换行会改变空白文本节点；正文内容另行比较，界面通过浏览器检查。

### .NET 基线限制及复测

完整 `SereinFlow.sln` 在修改前即因缺少 `src/ThirdParty/SereinScript/ScriptLangTest/ScriptLangTest.csproj` 而报 MSB3202。本次没有修改第三方项目或解决方案引用。使用本地 `.artifacts/available-projects.slnf` 排除这一个不存在的项目，构建其余 34 个原有项目。

初始测试基线为 467 通过、8 失败。部分集成测试固定寻找 Debug 版 Worker Runner；仅构建 Release 无法满足前置条件。补建 Debug Runner 后，使用干净 HEAD 源码在同一 Windows 环境重新构建并复测 API 集成测试，原始源码与修改后源码均为 21 通过、5 失败，失败用例一致：

- `ExternalHostIntegrationTests.ConflictingHostRouteFailsBeforeListening`
- `ExternalHostIntegrationTests.StricterHostUploadLimitFailsBeforeListening`
- `ExternalHostIntegrationTests.RegistrationDefersStorageAndRunnerValidationUntilStartup`
- `McpDebugIntegrationTests.McpDebugToolsRunAFullStepContinueAndStopLifecycle`
- `McpHostIntegrationTests.StdioModeFailsBeforeServingWhenTheExplicitKeyIsMissing`

前三项受到 Windows `.NET Runtime` EventLog 写入权限影响，后两项分别为调试等待超时和 stdio 子进程退出超时。原始源码也复现这些问题，未修改预期结果或扩大超时时间。不能将这组集成测试标记为全部通过。

复测期间，临时干净源码副本曾被架构测试的递归项目枚举计入，导致多出一个 MCP 项目的断言失败。副本移至 `.artifacts/bin/` 后，架构测试单独重跑 33/33 通过，随后又对最终代码运行全项目测试。原始全项目日志、最终日志和架构重跑日志均保留。

本地复现命令：

```powershell
./eng/check-code-conventions.ps1 -SelfTest
dotnet build .artifacts/available-projects.slnf --configuration Release --no-restore -m:1 -p:BuildInParallel=false
dotnet build src/SereinFlow.Worker.Runner/SereinFlow.Worker.Runner.csproj --configuration Debug --no-restore -m:1 -p:BuildInParallel=false
dotnet test .artifacts/available-projects.slnf --configuration Release --no-build --no-restore -m:1
dotnet test tests/SereinFlow.ArchitectureTests/SereinFlow.ArchitectureTests.csproj --configuration Release --no-build --no-restore
```

前端命令分别在 `frontend/sereinflow-web` 和 `frontend/sereinflow-docs` 中运行，并使用上述 Node/npm 版本。

## 语义审查

调用拆分保留短路分支、可选链的空值跳过、构造与对象初始化顺序、回调执行边界、异步等待、查询延迟执行和资源释放范围。构造函数 `this(...)` 中的调用改为在构造体内依次计算再赋值，保留 readonly 字段和资源归属；存储选项 record 保留原有主构造及公共属性，在兼容构造体中计算工件目录。反射任务结果仅在泛型返回类型分支中读取。SQL 表达式树的 GUID/枚举转换提前存入查询外的变量，原查询仍交由提供程序处理。

对 UUID 位掩码与分组、上传字节/编码换算、运行上限、调度队列限制、分页上限、重试指数、请求窗口、暂存寿命、Worker 等待时间、编译诊断上限、SignalR 协议字段、监听失败重试、完成轮询、变量协议限制和面板吸附阈值提取命名常量。配置默认值、显示文本、测试输入和类型字面量保留明确来源。常量含义需在后续评审中继续按 `code-conventions.md` 的语义规则检查，不能仅依赖语法扫描。

C# 例外清单为空。Vue 仅保留 `withDefaults` 第一个 `defineProps` 参数的必要编译宏结构，具体文件与位置登记在 `code-conventions.md`。

## 浏览器检查

使用隔离的本地数据目录 `.artifacts/ui-verification-data`，未操作原有业务数据。

- 主站桌面：创建测试项目、拖入脚本节点、设为入口、修改名称、保存、打开/关闭沉浸式编辑器、执行脚本并在运行控制台确认“已完成”。
- 运行控制台 390px：状态卡片、运行列表、横向滚动和队列导航正常，完成状态可见。
- 文档站桌面与 390px：中文/英文正文、导航、侧栏、语言切换及手机目录展开和链接跳转通过。手机首页布局和暂停/恢复动效按钮通过。最终构建后重启预览服务，确认使用最新静态资源。
- 主站编辑器 390px：停靠面板重叠、画布高度不足。使用原始源码单独构建并对照，原版和修改版的画布及面板边界相同。这是已存在的布局问题，本次保持原行为，手机编辑器不能标记为完整验收通过。

本地截图保存在 `.artifacts/screenshots/`：`editor-desktop.png`、`run-console-desktop.png`、`run-console-mobile.png`、`docs-desktop.png`、`docs-mobile.png`、`docs-home-mobile.png`。

## 本地证据

`.artifacts/` 为忽略目录，日志与测试数据不进入交付代码：

- `final-conventions.log`
- `final-web-tests.log`、`final-web-build.log`、`final-web-ci.log`
- `final-docs-build.log`
- `final-dotnet-build.log`、`final-dotnet-tests-complete.log`、`final-dotnet-tests-prerequisites.log`、`final-architecture-tests.log`
- `original-api-prerequisites-tests.log`、`final-api-prerequisites-tests.log`
- `final-api-audit.log`、`typescript-api-audit.log`、`style-audit.log`
- `test-results/*.trx`

验证标签页、预览服务、隔离 API 及测试遗留进程在结束前清理；保留忽略目录内的源码基线、测试数据和日志供复核。

CI 已配置但未在远端执行；完整解决方案的缺失项目、上述五项环境相关集成测试失败及手机编辑器的原有布局问题仍需单独处理。
