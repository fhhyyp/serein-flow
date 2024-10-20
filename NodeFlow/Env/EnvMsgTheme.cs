﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Env
{
    /// <summary>
    /// 消息主题
    /// </summary>
    public static class EnvMsgTheme
    {
        /// <summary>
        /// 获取远程环境信息
        /// </summary>
        public const string GetEnvInfo = nameof(GetEnvInfo);
        /// <summary>
        /// 尝试开始流程
        /// </summary>
        public const string StartFlow = nameof(StartFlow);
        /// <summary>
        /// 尝试从指定节点开始运行
        /// </summary>
        public const string StartFlowInSelectNode = nameof(StartFlowInSelectNode);
        /// <summary>
        /// 尝试结束流程运行
        /// </summary>
        public const string ExitFlow = nameof(ExitFlow);
        /// <summary>
        /// 尝试移动某个节点
        /// </summary>
        public const string MoveNode = nameof(MoveNode);
        /// <summary>
        /// 尝试设置流程起点
        /// </summary>
        public const string SetStartNode = nameof(SetStartNode);
        /// <summary>
        /// 尝试连接两个节点
        /// </summary>
        public const string ConnectNode = nameof(ConnectNode);
        /// <summary>
        /// 尝试创建节点
        /// </summary>
        public const string CreateNode = nameof(CreateNode);
        /// <summary>
        /// 尝试移除节点之间的连接关系
        /// </summary>
        public const string RemoveConnect = nameof(RemoveConnect);
        /// <summary>
        /// 尝试移除节点
        /// </summary>
        public const string RemoveNode = nameof(RemoveNode);
        /// <summary>
        /// 激活一个触发器
        /// </summary>
        public const string ActivateFlipflopNode = nameof(ActivateFlipflopNode);
        /// <summary>
        /// 终结一个触发器
        /// </summary>
        public const string TerminateFlipflopNode = nameof(TerminateFlipflopNode);

        /// <summary>
        /// 属性通知
        /// </summary>
        public const string ValueNotification = nameof(ValueNotification);



        /// <summary>
        /// 尝试获取项目信息
        /// </summary>
        public const string GetProjectInfo = nameof(GetProjectInfo);
        /// <summary>
        /// 尝试设置节点中断
        /// </summary>
        public const string SetNodeInterrupt = nameof(SetNodeInterrupt);
        /// <summary>
        /// 尝试添加中断表达式
        /// </summary>
        public const string AddInterruptExpression = nameof(AddInterruptExpression);
        /// <summary>
        /// 尝试设置节点/对象监视状态
        /// </summary>
        public const string SetMonitor = nameof(SetMonitor);

    }
}