using SereinFlow.Core.Api;

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
        await Task.Delay(Math.Clamp(轮询间隔毫秒, 50, 5_000));
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
}

public sealed record 批次质量结果(
    string 批次号,
    int 计划数量,
    int 检测数量,
    int 合格数量,
    decimal 合格率,
    bool 是否全部合格);
