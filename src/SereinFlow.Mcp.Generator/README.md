# MCP 源生成器

本项目使用 `src/ThirdParty/Serein.Generator.Framework` 的公共输出、符号、特性、常量及诊断能力。
工具、请求校验、属性绑定和宿主模型由本项目独立维护；不再复制 ScriptLang 的通用缓存模型。
框架 DLL 随 Analyzer 加载及打包，消费项目无需额外引用它，也不会引入运行时依赖。

处理流程：

0. `RegisterPostInitializationOutput` 注入 `McpToolkits/Output.cs` 中的声明模板，
   输出工具、上下文和参数特性，确保后续语义分析能够识别同一次编译中新生成的特性。
1. `McpToolGenerator.Predicate/Transform` 筛选带特性的方法，建立工具缓存。
2. `McpToolCacheExtension` 分析参数、元数据、构造函数和调用策略。
3. `McpValidationCacheExtension` 建立请求对象及嵌套属性的校验模型。
   `McpPropertyBindingCacheExtension` 为声明式参数建立字段读取、默认值与类型转换模型。
4. 在语义转换内使用 `CodeBuilder<T>` 输出工具片段与请求支持代码，形成不持有符号的 `McpToolAnalysis`。
   汇总阶段按工具名称排序，按请求类型去重支持代码，再输出目录、注册工厂和直接调用。
5. `McpValidationExtension` 根据模型输出直接访问属性的递归校验。
   `McpPropertyBindingExtension` 输出请求字段绑定，并缓存参数元数据。
6. `McpHostCacheExtension` 分析 `[McpHost]` 声明，建立 `McpHostCache`；
   `McpHostExtension` 输出注册与目录入口，并声明可选的 partial 配置钩子。

入口类不拼接生成代码。工具名称重复、方法签名不支持、构造函数不明确或宿主声明不合法时，报告 MCPGEN001–004 编译错误。
生成目录位于当前程序集命名空间的 `Generated.McpToolRegistry` 中。
生成器只作为 Analyzer 使用，不进入 MCP 运行时依赖图。
工具发现保留用户自定义派生特性的语义匹配；宿主使用精确特性发现，避免多个 partial 声明重复输出。
增量步骤使用值相等结果比较；无关编辑可以复用输出，独立 DTO 文件的修改会更新请求绑定。
工具方法不接受原始 `JsonElement` 请求；动态 JSON 只能作为强类型请求的字段。

声明特性均为消费程序集内部类型，框架通过 `IMcpToolMetadata` 读取运行时元数据，
生成参数规则使用独立的 `McpParameterOptions`，不让生成类型进入跨程序集 API。
`SEREINFLOW_MCP_HOST` 编译常量启用可选的 SereinFlow 工具策略特性模板；
通用宿主不设置该常量，不需要 SereinFlow.Contracts 或其他业务依赖。
框架和业务项目均不再手写上述特性类型。

运行 `eng/verify-generator-packages.ps1`，可验证本生成器与 ScriptLang 同时通过项目引用和 NuGet 使用，
并实际执行生成入口。`SereinFlow.Mcp.Generator.Tests` 另行覆盖诊断、编译与增量编辑行为。
