using Serein.Workbench.Node.View;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Serein.Workbench.Node
{
    /// <summary>
    /// 约束一个节点应该有哪些控制点
    /// </summary>
    public interface INodeJunction
    {
        /// <summary>
        /// 方法执行入口控制点
        /// </summary>
        JunctionControlBase ExecuteJunction {  get; }
        /// <summary>
        /// 执行完成后下一个要执行的方法控制点
        /// </summary>
        JunctionControlBase NextStepJunction {  get; }

        /// <summary>
        /// 参数节点控制点
        /// </summary>
        JunctionControlBase[] ArgDataJunction { get; }
        /// <summary>
        /// 返回值控制点
        /// </summary>
        JunctionControlBase ReturnDataJunction { get; }

        /// <summary>
        /// 获取目标参数控制点，用于防止wpf释放资源导致找不到目标节点，返回-1,-1的坐标
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        JunctionControlBase GetJunctionOfArgData(int index)
        {
            return ArgDataJunction[index];
        }
    }
}
