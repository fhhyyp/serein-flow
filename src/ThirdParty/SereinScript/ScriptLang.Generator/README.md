# ScriptLang 原型生成器

使用 `Serein.Generator.Framework` 的初始化注入、符号工具、常量输出、代码构建、诊断快照和增量输出能力。
生成器只保留脚本原型分析与模板，不再维护复制的通用缓存模型。

消费项目引用 ScriptLang 运行时，并将本项目作为 Analyzer 引用：

```xml
<ProjectReference Include="路径/ScriptLang.Generator.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

NuGet 消费时只需安装生成器包；生成期的框架 DLL 已随包放入 Analyzer 目录。
`PrototypeExtension`、`PrototypeProperty`、`PrototypeFunction` 和 `NamingFormat` 的声明名称保持不变。

在顶层、非泛型的 partial class 上声明 `[PrototypeExtension]`，
实现 `public partial bool IsTarget(Value value)`，再使用属性或函数特性标记成员。
原型可访问全部 partial 声明中的方法；CLR 重载应使用不同的 `Name` 别名。
同一方法可以声明多个具有不同 `Name` 的特性以提供多个脚本别名。
全局、文件作用域与多层命名空间均可使用；生成文件按类型完整身份命名，避免同名类冲突。
嵌套类、泛型类、静态类或非 partial 类会报告明确诊断。

`PushThis`、Net/Js 命名方式、显式别名、脚本值与 CLR 接收者转换、同步及 Task/ValueTask 调用继续由脚本生成器维护。
异步调用根据返回类型识别，不要求源方法带 `async` 修饰符；ScriptEngine 参数不计入用户参数数目。
已有可选脚本参数省略时仍使用 default 值。

诊断：`Script001` 表示缺少 IsTarget 分部实现；`PROTOTYPE002` 表示不支持的声明形状；
`PROTOTYPE003` 表示重复的脚本成员名称；`SGENFW001` 表示生成器意外异常。
取消请求直接传播，意外异常不会再被静默吞掉。

测试覆盖生成代码编译、诊断、partial、重载别名、命名空间、同名类型与增量编辑。
运行 `eng/verify-generator-packages.ps1` 可额外验证与 MCP 生成器同时使用的项目引用和 NuGet 消费。
