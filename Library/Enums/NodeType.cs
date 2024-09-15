﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Enums
{
    public enum NodeType
    {
        /// <summary>
        /// 初始化（事件，不生成节点）
        /// </summary>
        Init,
        /// <summary>
        /// 开始载入（事件，不生成节点）
        /// </summary>
        Loading,
        /// <summary>
        /// 结束（事件，不生成节点）
        /// </summary>
        Exit,

        /// <summary>
        /// 触发器
        /// </summary>
        Flipflop,
        /// <summary>
        /// 条件
        /// </summary>
        Condition,
        /// <summary>
        /// 动作
        /// </summary>
        Action,
    }

    /// <summary>
    /// 生成的节点控件
    /// </summary>
    public enum NodeControlType
    {
        None,
        /// <summary>
        /// 动作节点
        /// </summary>
        Action,
        /// <summary>
        /// 触发器节点
        /// </summary>
        Flipflop,
        /// <summary>
        /// 表达式操作节点
        /// </summary>
        ExpOp,
        /// <summary>
        /// 表达式操作节点
        /// </summary>
        ExpCondition,
        /// <summary>
        /// 条件节点区域
        /// </summary>
        ConditionRegion,
    }

}
