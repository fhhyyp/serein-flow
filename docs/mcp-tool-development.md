# MCP 工具开发

独立项目 `SereinFlow.Mcp.Framework` 负责加载生成目录、绑定 JSON、校验参数和执行工具调用。
它只依赖 .NET 与 Microsoft.Extensions.DependencyInjection，不引用任何 SereinFlow 业务项目。

运行时引用方向：`SereinFlow.Mcp → SereinFlow.Mcp.Framework`。
`SereinFlow.Mcp.Generator` 通过 Analyzer 项目引用在编译期生成目录、DI 工厂、元数据和直接调用代码。
运行时不再扫描程序集、读取方法特性或构建表达式调用委托。
工具描述、目录、定义和执行器统一由框架提供，业务项目不再定义第二套工具模型或转换目录。
`SereinFlow.Mcp/Integration` 只包含业务策略：`SereinFlowMcpHosting` 配置框架，
`ISereinFlowMcpToolMetadata` 定义运行时权限与审计合同，`SereinFlowMcpPolicy` 继承
框架的 `McpToolPolicy`，只实现授权、业务结果判定和协议错误映射。
框架的 `McpToolExecutor` 负责作用域、查找和中间件执行顺序。
`McpToolPolicy` 统一管理授权后执行、串行闸门、耗时、失败分类和完成记录；
完成记录在独立的异步作用域执行，其失败不会覆盖工具结果或原始异常。
`McpToolMutationGate` 串行执行要求幂等键的同名工具，业务不再维护信号量和租约。
`SereinFlowMcpAuditProjection` 只负责将完成记录投影为业务审计字段并写入存储。
业务 JSON 配置和调用者上下文由宿主传入，框架不认识项目 ID、API Key 或业务权限。
通用手工 Schema 模型 `McpJsonSchema` 也由框架提供，业务只保留实际工具的 Schema 定义。

`McpToolAttribute`、`McpContextAttribute`、`McpBindPropertiesAttribute`、`McpParameterAttribute`、`McpHostAttribute`
由生成器在 `RegisterPostInitializationOutput` 中输出为 `McpToolkits.g.cs`，供同一次编译的语义分析使用。
这些类型都是当前程序集的 internal 声明；框架运行时只依赖 `IMcpToolMetadata` 和 `McpParameterOptions`，
不依赖某个宿主生成的特性类型，也不在调用时反射读取特性。
SereinFlow 宿主在项目中设置 `DefineConstants` 包含 `SEREINFLOW_MCP_HOST`，启用
`SereinFlowMcpToolkits.g.cs` 中的内部 `SereinFlowMcpToolAttribute`，实现宿主的 `ISereinFlowMcpToolMetadata`。
独立宿主无需此常量，也无需引用 SereinFlow 业务合同；自定义特性应在当前工具程序集内派生。

## 宿主入口

在顶层 `partial static class` 上声明 `[McpHost("AddSereinFlowMcpTools")]`，
SG 将生成服务注册和 `CreateCatalog` 的默认目录及显式目录重载。
三个可选的 partial 方法保留业务配置：

```csharp
[McpHost("AddExampleTools")]
public static partial class ExampleHosting
{
    static partial void ConfigureServices(IServiceCollection services)
    {
        // 注册业务服务和继承 McpToolPolicy 的宿主策略。
    }

    static partial void ConfigureJsonOptions(ref JsonSerializerOptions options)
        => options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    static partial void ConfigureCatalog(McpToolCatalog catalog)
    {
        // 检查宿主要求的权限或其他业务合同。
    }
}
```

无需配置时可使用 `[McpHost]`，默认注册方法为 `AddMcpTools`。
直接调用 `McpToolDefinition.ExecuteAsync` 不经过策略与串行闸门；完整调用使用 `McpToolExecutor`。

## 添加工具

在 `SereinFlow.Mcp` 程序集中添加工具类和请求类型即可，不需要修改集中注册表。
工具依赖通过构造函数注入，生成器会生成 Scoped 注册和直接构造函数调用，
由每次工具调用的作用域管理释放。工具类应只有一个 public/internal 构造函数，工具方法返回 `Task<T>`。

```csharp
internal sealed class ProjectTools(AiReadModelService projects)
{
    [SereinFlowMcpTool("example_get_project", McpPermissionDto.ProjectRead,
        Description = "Read a project and its flow summaries.")]
    public async Task<object?> GetAsync(
        ProjectRequest request,
        CancellationToken cancellationToken)
    {
        return await projects.GetProjectAsync(request.ProjectId, cancellationToken);
    }
}

[McpBindProperties]
internal sealed class ProjectRequest
{
    [McpParameter(Required = true)]
    public Guid ProjectId { get; set; }
}
```

上面的 `projectId` 会被基础权限管线用于检查项目访问范围。通过 runId、previewId 等间接定位项目的工具，
仍须在定位实际资源后检查相应项目权限，不能只依赖入口的权限声明。

方法返回 `Task<T>`，接受至多一个请求对象，并可选接受 `SereinFlowMcpCaller` 和
`CancellationToken`。调用者信息由框架提供，不属于客户端参数；服务通过构造函数注入。
自定义宿主上下文类型或参数使用 `[McpContext]` 标记。
生成的委托直接调用工具方法，不会给业务异常套上反射调用异常。

所有工具都接收强类型请求；工具方法直接接收 `JsonElement` 会触发 MCPGEN002 编译错误。
普通 DTO 使用既有合同 JSON 配置自动反序列化，包括 camelCase 字段和合同转换器。
带 `[McpBindProperties]` 的请求由 SG 根据属性生成 JSON 字段读取和类型转换，缺失的可选字段保留属性初始值。
这种请求使用无参构造函数与可写 `set` 属性；`[McpParameter]` 声明必填、Trim、null 和错误码等规则。
请求可使用 `Required`、`Range` 等 DataAnnotations，以及 `IValidatableObject`；
嵌套对象、集合和字典值通过生成的属性访问代码校验，不在调用时枚举 `PropertyInfo`。
普通 DTO 缺少可能采用默认值的值类型字段时，可使用 nullable 加 `Required`，
或 C# `required` 明确要求字段存在。生成式属性请求使用 `McpParameter.Required`。
跨资源、跨数据库的业务规则由业务服务校验。

```csharp
internal sealed record RenameRequest(
    [property: Required] string Name,
    [property: Required] Guid? ProjectId,
    [property: Required] string IdempotencyKey);
```

允许任意 JSON 内容的字段仍可使用 `JsonElement`，例如消息的 `Payload`、变量的 `Value`。
用 `[McpParameter(Required = true, AllowNull = true)]` 表达“字段必须存在，但显式 null 合法”；
框架负责缺失检查和提取，业务方法不再调用 `TryGetProperty`。
需要兼容旧协议或归一化为业务请求时，可在宿主绑定层注册 `IMcpRequestBinder<T>`。
Flow Patch 的 v1/v2 兼容由 `FlowPatchRequestBinder` 调用既有归一化服务完成，处理器只接收 `NormalizedFlowPatchRequest`。

写入工具在特性上声明 `ExecutionKind = McpToolExecutionKind.Mutation`；需要幂等键时再声明
`RequiresIdempotencyKey = true`。这会启用既有的并发闸门；持久化幂等回放仍使用
`McpIdempotencyService`，特性不会自动代替业务事务或保存回放结果。
多个权限参数表示满足其中一个权限，与原先 ReadAny 的含义一致。

## Schema 与旧工具

新工具从请求类型通过 System.Text.Json 生成 Schema，无参数方法生成空对象 Schema。
Schema 采用序列化合同；DataAnnotations 是运行时校验规则，不保证每个规则都会导出为 Schema 约束。
生成式属性请求的 `McpParameter.Required` 会合并到自动生成 Schema 的 required 列表。
生成器只处理当前编译程序集中的工具，不自动加载插件。

旧工具继续使用已经发布的显式 Schema，保持描述、字段、必填列表和工具顺序。
现有 50 个工具已全部使用强类型参数，通过同一框架管线执行；上下文优先使用
不暴露服务容器的 `SereinFlowMcpCaller`。`SereinFlowMcpToolContext` 继承调用者身份，并补充业务服务访问。
现有完整范例为 `McpProjectQueryTools`，
带幂等的强类型写入范例为 `McpApiKeyToolHandlers.RevokeAsync`。

`McpToolDiscovery.CreateCatalog` 使用生成的 `IMcpToolRegistry`，并可按处理器类型筛选，便于独立测试。
业务宿主通过 `SereinFlowMcpHosting` 配置框架，默认使用 `SereinFlow.Mcp.Generated.McpToolRegistry`；独立宿主通过
`AddMcpToolHandlers(registry)` 注册同一份生成目录。
重复工具名称（MCPGEN001）、不支持的方法签名（MCPGEN002）、不明确的构造函数（MCPGEN003）、不支持的宿主声明（MCPGEN004）
会在编译时报告错误；显式旧合同缺少对应工具时在加载目录时报错。

## 在其他项目使用框架

引用 `SereinFlow.Mcp.Framework.csproj`，并添加 `SereinFlow.Mcp.Generator.csproj` 的 Analyzer 引用：

```xml
<ProjectReference Include="...\SereinFlow.Mcp.Generator.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

使用 `SereinFlow.Mcp.Framework` 命名空间，实例化生成的 `<程序集命名空间>.Generated.McpToolRegistry`。
通用 `[McpTool("name", Description = "...")]` 不包含 SereinFlow 的权限类型。
宿主通过 `McpToolDiscoveryOptions` 配置自己的 JSON 合同，通过 `[McpContext]` 标记上下文参数类型，
通过 `McpToolCallContext` 提供当前作用域的服务容器与上下文值。
框架抛出 `McpParameterBindingException` 表达绑定和校验失败，由宿主转换为自己的协议错误。
宿主通过 `IMcpToolMiddleware` 实现业务授权、审计等策略；框架执行器管理调用作用域并异步释放依赖。
常规宿主可继承 `McpToolPolicy`，分别覆盖 `AuthorizeAsync`、`ProcessResult`、`MapFailure`、`RecordAsync`，
复用通用调用生命周期。`RecordAsync` 收到独立审计作用域的服务容器和 `McpToolCompletion`，
工具调用失败时 `Value` 为 null，`Failure` 包含宿主映射后的异常、结果分类和摘要。
直接使用 `McpToolDefinition.ExecuteAsync` 时，由调用方提供和释放作用域。
框架不提供 HTTP/stdio 传输，也不包含业务权限、持久化幂等和业务审计存储实现。

## 生成器组织方式

`SereinFlow.Mcp.Generator` 将 ScriptLang.Generator 的通用 Models、Extensions 和 GeneratorConfig
完整迁入自身命名空间，不通过 `Compile Include` 或 `Link` 引用上游源码。
入口 `McpToolGenerator` 只负责 Predicate、Transform、缓存模型和输出注册。
`McpToolkits/Output.cs` 保存初始化输出模板，采用与 ScriptLang.Generator 相同的初始化注入方式。
`McpToolCacheExtension` 构建工具模型，`McpValidationCacheExtension` 构建请求属性图；
`McpToolRegistryExtension` 和 `McpValidationExtension` 通过 `GeneratorCache<T>` 的链式扩展生成代码。
`McpPropertyBindingCacheExtension` 构建属性绑定模型，`McpPropertyBindingExtension` 生成参数读取代码。
`McpHostCacheExtension` 构建宿主模型，`McpHostExtension` 输出注册入口和配置钩子。
生成器不会改动原有 ScriptLang.Generator。

当前改造消除了工具发现、DI 激活、工具分发和嵌套属性遍历中的显式反射。
普通 DTO 绑定与 Schema 仍使用 System.Text.Json 的合同元数据，生成式属性绑定直接读取 JSON 字段，
DataAnnotations 仍使用其自身的校验实现；
这里不宣称整个序列化和校验依赖链都不使用反射。

## 验证

修改底层基础设施后运行 `SereinFlow.Mcp.Tests`，其中 `McpSchemaCompatibilityTests` 检查旧合同快照，
`McpToolDiscoveryTests` 检查新工具开发入口、参数校验、DI 作用域和异常行为。
`SereinFlow.Mcp.Framework.Tests` 的运行时只引用框架项目，验证其可脱离 SereinFlow 业务独立运行。
`SereinFlow.Mcp.Generator.Tests` 使用 Roslyn 验证生成代码可编译及编译期诊断。
