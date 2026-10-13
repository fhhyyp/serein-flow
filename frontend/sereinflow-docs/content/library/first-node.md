# 写出第一个节点

我们来写一个“计算合格率”节点。输入合格数量和检测总数，节点返回百分比。完成后它会像其他节点一样出现在项目的节点库里。

## 1. 建一个类库

当前 `SereinFlow.Library` 项目以 .NET 10 为目标。下面从 **SereinFlow 仓库根目录**创建一个练习项目，并直接引用仓库中的当前接口：

```powershell
dotnet new classlib -n MyFlowNodes -f net10.0
Set-Location MyFlowNodes
dotnet add reference ..\src\SereinFlow.Library\SereinFlow.Library.csproj
```

如果你在仓库外独立开发，也可以引用 `SereinFlow.Library` 包。请按服务端所用版本选择包；本仓库当前 `SereinFlow.Library.csproj` 写的版本是 `1.2.0`。

## 2. 写一个方法

在 `QualityNodes.cs` 中写入：

```csharp
using SereinFlow.Library;

namespace MyFlowNodes;

[FlowLibrary("质量检查")]
public sealed class QualityNodes
{
    [FlowNode(AnotherName = "计算合格率", Desc = "根据合格数和总数计算百分比")]
    public decimal PassRate(
        [NodeParam(Name = "合格数量")] int passed,
        [NodeParam(Name = "检测总数")] int total)
    {
        if (total <= 0)
            throw new ArgumentOutOfRangeException(nameof(total), "检测总数必须大于 0");

        return Math.Round((decimal)passed / total * 100m, 2);
    }
}
```

`[FlowLibrary]` 告诉 SereinFlow 这个类放着节点。`[FlowNode]` 让 `PassRate` 出现在画布的节点目录；`AnotherName` 和 `Desc` 是用户看到的名称和说明。`[NodeParam]` 指定输入框上的文字。方法返回的 `decimal` 就是节点结果。

## 3. 编译并放进流程

在 `MyFlowNodes.csproj` 的 `<PropertyGroup>` 中加入 `<EnableDynamicLoading>true</EnableDynamicLoading>`，让发布输出带上动态加载需要的依赖。然后从项目目录运行下面的 PowerShell 命令：

```powershell
dotnet publish .\MyFlowNodes.csproj -c Release -o ..\artifacts\MyFlowNodes-1.0.0
Compress-Archive -Path ..\artifacts\MyFlowNodes-1.0.0 `
  -DestinationPath ..\artifacts\MyFlowNodes-1.0.0.zip -Force
```

压缩包里第一层必须是 `MyFlowNodes-1.0.0` 目录；目录内必须有 `MyFlowNodes.dll`。包名、目录名和程序集名要对应。随后在控制台上传 ZIP，再把这份类库加入目标项目。完整结构和排错方法见[打包、上传与排错](/library/package-upload)。

回到项目画布，在节点库找到“计算合格率”，填入两项数字，把它设为入口，保存并运行。运行输出会显示结果。

::: tip 为什么没有手写节点 ID？
普通节点可以省略 `FlowNode.Id` 和 `NodeParam.Id`。当前接口会根据类库名、方法名和参数名生成它们。若已经有流程在使用这个节点，重命名时要考虑旧流程的参数对应关系；可以用明确的 ID 和 `NodeParam.Aliases` 做有意的迁移。
:::
