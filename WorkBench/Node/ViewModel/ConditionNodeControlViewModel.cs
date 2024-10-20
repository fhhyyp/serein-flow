﻿using Serein.NodeFlow.Model;
using Serein.Workbench.Node.View;

namespace Serein.Workbench.Node.ViewModel
{
    /// <summary>
    /// 条件节点
    /// </summary>
    public class ConditionNodeControlViewModel : NodeControlViewModelBase
    {
        public new SingleConditionNode NodeModel { get; }

        ///// <summary>
        ///// 是否为自定义参数
        ///// </summary>
        //public bool IsCustomData
        //{
        //    get => Node.IsCustomData;
        //    set { Node.IsCustomData= value; OnPropertyChanged(); }
        //}
        ///// <summary>
        // /// 自定义参数值
        // /// </summary>
        //public object? CustomData
        //{
        //    get => Node.CustomData;
        //    set { Node.CustomData = value ; OnPropertyChanged(); }
        //}
        ///// <summary>
        ///// 表达式
        ///// </summary>
        //public string Expression
        //{
        //    get => Node.Expression;
        //    set { Node.Expression = value; OnPropertyChanged(); }
        //}

        /// <summary>
        /// 条件节点
        /// </summary>
        /// <param name="node"></param>
        public ConditionNodeControlViewModel(SingleConditionNode node) : base(node)
        {
            this.NodeModel = node;
            //IsCustomData = false;
            //CustomData = "";
            //Expression = "PASS";
        }

    }
}
