using Serein.Library.Api;
using Serein.Library.Utils;
using System;
using System.Linq;

namespace Serein.Library
{

    /// <summary>
    /// 每个节点有独自的MethodDetails实例
    /// </summary>
    [AutoProperty(ValuePath = nameof(MethodDetails))]
    public partial class MethodDetails
    {
        private readonly IFlowEnvironment env;
        private readonly NodeModelBase nodeModel;
        /// <summary>
        /// 是否保护参数（目前仅视觉效果参数，不影响运行实现，后续将设置作用在运行逻辑中）
        /// </summary>
        [PropertyInfo(IsNotification = true)]
        private bool _isProtectionParameter;

        /// <summary>
        /// 作用实例的类型（多个相同的节点将拥有相同的类型）
        /// </summary>
        [PropertyInfo]
        private Type _actingInstanceType;

        /// <summary>
        /// 作用实例（多个相同的节点将会共享同一个实例）
        /// </summary>
        [PropertyInfo]
        private object _actingInstance;

        /// <summary>
        /// 方法名称
        /// </summary>
        [PropertyInfo]
        private string _methodName;

        /// <summary>
        /// 节点类型
        /// </summary>
        [PropertyInfo]
        private NodeType _methodDynamicType;

        /// <summary>
        /// 锁名称（暂未实现）
        /// </summary>
        [PropertyInfo]
        private string _methodLockName;


        /// <summary>
        /// 方法说明
        /// </summary>
        [PropertyInfo]
        private string _methodTips;


        /// <summary>
        /// 参数描述
        /// </summary>
        [PropertyInfo]
        private ParameterDetails[] _parameterDetailss;

        /// <summary>
        /// 出参类型
        /// </summary>
        [PropertyInfo]
        private Type _returnType;
    }


    public partial class MethodDetails
    {
        /// <summary>
        /// 不包含方法信息的基础节点（后续可能要改为DLL引入基础节点）
        /// </summary>
        public MethodDetails()
        {
            
        }
        /// <summary>
        /// 生成元数据
        /// </summary>
        /// <param name="env">节点运行的环境</param>
        /// <param name="nodeModel">标识属于哪个节点</param>
        public MethodDetails(IFlowEnvironment env, NodeModelBase nodeModel)
        {
            this.nodeModel = nodeModel;
        }

       
        /// <summary>
        /// 从方法信息中读取
        /// </summary>
        /// <param name="Info"></param>
        public MethodDetails(MethodDetailsInfo Info)
        {
            if (!Info.NodeType.TryConvertEnum<NodeType>(out var nodeType))
            {
                throw new ArgumentException("无效的节点类型");
            }
            MethodName = Info.MethodName;
            MethodTips = Info.MethodTips;
            MethodDynamicType = nodeType;
            ReturnType = Type.GetType(Info.ReturnTypeFullName);
            ParameterDetailss = Info.ParameterDetailsInfos.Select(pinfo => new ParameterDetails(pinfo)).ToArray();
        }

        /// <summary>
        /// 转为信息
        /// </summary>
        /// <returns></returns>
        public MethodDetailsInfo ToInfo()
        {
            return new MethodDetailsInfo
            {
                MethodName = MethodName,
                MethodTips = MethodTips,
                NodeType = MethodDynamicType.ToString(),
                ParameterDetailsInfos = ParameterDetailss.Select(p => p.ToInfo()).ToArray(),
                ReturnTypeFullName = ReturnType.FullName,
            };
        }

        /// <summary>
        /// 从DLL拖动出来时拷贝属于节点的实例
        /// </summary>
        /// <returns></returns>
        public MethodDetails CloneOfNode(IFlowEnvironment env, NodeModelBase nodeModel)
        {
            var md = new MethodDetails(env, nodeModel) // 创建新节点时拷贝实例
            {
                ActingInstance = this.ActingInstance,
                ActingInstanceType = this.ActingInstanceType,
                MethodDynamicType = this.MethodDynamicType,
                MethodTips = this.MethodTips,
                ReturnType = this.ReturnType,
                MethodName = this.MethodName,
                MethodLockName = this.MethodLockName,
                IsProtectionParameter = this.IsProtectionParameter,
            };
            md.ParameterDetailss = this.ParameterDetailss.Select(p => p.CloneOfClone(env, nodeModel)).ToArray(); // 拷贝属于节点方法的新入参描述
            return md;
        }





        ///// <summary>
        ///// 每个节点有独自的MethodDetails实例
        ///// </summary>
        //public partial class TmpMethodDetails
        //{
        //    /// <summary>
        //    /// 是否保护参数（目前仅视觉效果参数，不影响运行实现，后续将设置作用在运行逻辑中）
        //    /// </summary>
        //    public bool IsProtectionParameter { get; set; } = false;

        //    /// <summary>
        //    /// 作用实例的类型（多个相同的节点将拥有相同的类型）
        //    /// </summary>
        //    public Type ActingInstanceType { get; set; }

        //    /// <summary>
        //    /// 作用实例（多个相同的节点将会共享同一个实例）
        //    /// </summary>
        //    public object ActingInstance { get; set; }

        //    /// <summary>
        //    /// 方法名称
        //    /// </summary>
        //    public string MethodName { get; set; }

        //    /// <summary>
        //    /// 节点类型
        //    /// </summary>
        //    public NodeType MethodDynamicType { get; set; }

        //    /// <summary>
        //    /// 锁名称（暂未实现）
        //    /// </summary>
        //    public string MethodLockName { get; set; }


        //    /// <summary>
        //    /// 方法说明
        //    /// </summary>
        //    public string MethodTips { get; set; }


        //    /// <summary>
        //    /// 参数描述
        //    /// </summary>

        //    public ParameterDetails[] ParameterDetailss { get; set; }

        //    /// <summary>
        //    /// 出参类型
        //    /// </summary>

        //    public Type ReturnType { get; set; }
        //}

    }

}
