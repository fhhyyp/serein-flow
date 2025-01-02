﻿using Serein.Library;
using Serein.Workbench.Avalonia.Custom.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.Avalonia.Api
{
  


    /// <summary>
    /// 约束一个节点应该有哪些控制点
    /// </summary>
    public interface INodeJunction
    {
        /// <summary>
        /// 方法执行入口控制点
        /// </summary>
        NodeJunctionView ExecuteJunction { get; }
        /// <summary>
        /// 执行完成后下一个要执行的方法控制点
        /// </summary>
        NodeJunctionView NextStepJunction { get; }

        /// <summary>
        /// 参数节点控制点
        /// </summary>
        NodeJunctionView[] ArgDataJunction { get; }
        /// <summary>
        /// 返回值控制点
        /// </summary>
        NodeJunctionView ReturnDataJunction { get; }

        /// <summary>
        /// 获取目标参数控制点，用于防止wpf释放资源导致找不到目标节点，返回-1,-1的坐标
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        NodeJunctionView GetJunctionOfArgData(int index)
        {
            var arr = ArgDataJunction;
            if (index >= arr.Length)
            {
                return null;
            }
            return arr[index];
        }
    }
}