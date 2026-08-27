using SereinFlow.Core.Api;
using SereinFlow.Runtime.Abstractions;

namespace SereinFlow.TestLibrary;

[FlowLibrary("生产线设备与质量数据示例库")]
public sealed class 生产线节点
{
    [FlowNode(AnotherName = "计算合格率", Desc = "根据合格数量和检测总数计算本批次合格率。")]
    public decimal 计算合格率(
        [NodeParam(Name = "合格数量")] int 合格数量,
        [NodeParam(Name = "检测总数")] int 检测总数)
    {
        if (检测总数 <= 0)
            throw new ArgumentOutOfRangeException(nameof(检测总数), "检测总数必须大于 0。");

        return Math.Round((decimal)合格数量 / 检测总数 * 100m, 2);
    }

    [FlowNode(AnotherName = "构建设备写入指令", Desc = "将设备编号、点位和数值组合为可发送给设备适配器的写入指令。")]
    public string 构建设备写入指令(
        [NodeParam(Name = "设备编号")] string 设备编号,
        [NodeParam(Name = "点位名称")] string 点位名称,
        [NodeParam(Name = "写入值")] decimal 写入值,
        [NodeParam(Name = "保留小数位")] int 保留小数位 = 2)
        => $"{设备编号}:{点位名称}={Math.Round(写入值, Math.Clamp(保留小数位, 0, 6))}";

    [FlowNode(AnotherName = "记录工序结果", Desc = "记录工单在指定工序中的合格状态和备注，不产生数据输出。")]
    public void 记录工序结果(
        [NodeParam(Name = "工单号")] string 工单号,
        [NodeParam(Name = "工序名称")] string 工序名称,
        [NodeParam(Name = "是否合格")] bool 是否合格,
        [NodeParam(Name = "备注")] string? 备注 = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(工单号);
        ArgumentException.ThrowIfNullOrWhiteSpace(工序名称);
        _ = 是否合格;
        _ = 备注;
    }

    [FlowNode(NodeType = NodeType.Flipflop, AnotherName = "等待设备触发", Desc = "模拟设备轮询触发；任务完成后可调度下游流程。")]
    public async Task<bool> 等待设备触发(
        [NodeParam(Name = "设备编号")] string 设备编号,
        [NodeParam(Name = "轮询间隔毫秒")] int 轮询间隔毫秒 = 200)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(设备编号);
        // Keep the lower bound so a malformed setting cannot create a busy
        // loop, while allowing production-like device polling intervals.
        // 保留最小值以避免错误配置导致忙循环，同时允许符合实际设备场景的轮询间隔。
        await Task.Delay(Math.Clamp(轮询间隔毫秒, 50, 3_600_000));
        return true;
    }

    [FlowNode(AnotherName = "汇总批次质量", Desc = "汇总批次计划数、检测数和合格数，返回结构化质量结果。")]
    public 批次质量结果 汇总批次质量(
        [NodeParam(Name = "批次号")] string 批次号,
        [NodeParam(Name = "计划数量")] int 计划数量,
        [NodeParam(Name = "检测数量")] int 检测数量,
        [NodeParam(Name = "合格数量")] int 合格数量)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(批次号);
        if (计划数量 < 0 || 检测数量 < 0 || 合格数量 < 0 || 合格数量 > 检测数量)
            throw new ArgumentOutOfRangeException(nameof(合格数量), "批次数量数据不合法。");

        var 合格率 = 检测数量 == 0 ? 0m : Math.Round((decimal)合格数量 / 检测数量 * 100m, 2);
        return new 批次质量结果(批次号, 计划数量, 检测数量, 合格数量, 合格率, 检测数量 >= 计划数量 && 合格数量 == 检测数量);
    }

    [FlowNode(AnotherName = "按设备状态选择分支", Desc = "根据设备状态选择成功、失败或错误分支，演示受限流程上下文注入。")]
    public string 按设备状态选择分支(
        [NodeParam(Name = "设备状态")] string 设备状态,
        IFlowContext 流程上下文)
    {
        return 设备状态 switch
        {
            "就绪" => "设备已就绪",
            "告警" => 选择失败(流程上下文),
            "故障" => 选择错误(流程上下文),
            _ => 选择失败(流程上下文)
        };
    }

    [FlowNode(AnotherName = "汇总多个检测值", Desc = "接收可变数量的检测值并返回合计，演示 params 参数。")]
    public int 汇总多个检测值([NodeParam(Name = "检测值")] params int[] 检测值)
        => 检测值.Sum();

    [FlowNode(AnotherName = "设置设备运行模式", Desc = "设置设备在自动、手动或维护模式下运行，演示普通枚举输入。")]
    public string 设置设备运行模式([NodeParam(Name = "运行模式")] 设备运行模式 运行模式)
        => $"当前运行模式：{运行模式}";

    [FlowNode(AnotherName = "配置设备操作权限", Desc = "配置设备可执行的操作权限，演示 Flags 枚举输入。")]
    public string 配置设备操作权限([NodeParam(Name = "操作权限")] 设备操作权限 操作权限)
        => $"当前操作权限：{操作权限}";

    [FlowNode(AnotherName = "生成设备配置摘要", Desc = "同时接收运行模式和操作权限，便于验证普通枚举下拉、Flags 多选、保存重载及节点输出。")]
    public 设备配置摘要 生成设备配置摘要(
        [NodeParam(Name = "运行模式")] 设备运行模式 运行模式,
        [NodeParam(Name = "操作权限")] 设备操作权限 操作权限)
        => new(运行模式, 操作权限, DateTimeOffset.UtcNow);

    [FlowNode(NodeType = NodeType.Flipflop, AnotherName = "监听设备配置变更", Desc = "以异步监听方式返回当前设备枚举配置，用于验证 Flipflop 的枚举参数转换与 Task<T> 输出。")]
    public async Task<设备配置摘要> 监听设备配置变更(
        [NodeParam(Name = "运行模式")] 设备运行模式 运行模式,
        [NodeParam(Name = "操作权限")] 设备操作权限 操作权限,
        [NodeParam(Name = "监听间隔毫秒")] int 监听间隔毫秒 = 1000)
    {
        await Task.Delay(Math.Clamp(监听间隔毫秒, 50, 3_600_000));
        return new 设备配置摘要(运行模式, 操作权限, DateTimeOffset.UtcNow);
    }

    private static string 选择失败(IFlowContext 流程上下文)
    {
        流程上下文.SelectFailure("device.not_ready", "Device is not ready. 设备未就绪。");
        return "设备未就绪";
    }

    private static string 选择错误(IFlowContext 流程上下文)
    {
        流程上下文.SelectError("device.faulted", "Device faulted. 设备发生故障。");
        return "设备故障";
    }
}

public sealed record 批次质量结果(
    string 批次号,
    int 计划数量,
    int 检测数量,
    int 合格数量,
    decimal 合格率,
    bool 是否全部合格);

public sealed record 设备配置摘要(
    设备运行模式 运行模式,
    设备操作权限 操作权限,
    DateTimeOffset 生效时间);

public enum 设备运行模式
{
    自动 = 0,
    手动 = 1,
    维护 = 2,
}

[Flags]
public enum 设备操作权限 : ulong
{
    无 = 0,
    读取状态 = 1,
    写入参数 = 2,
    执行诊断 = 4,
    全部 = 7,
}
