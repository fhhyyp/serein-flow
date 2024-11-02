using Serein.Library.Api;
using Serein.Library.Utils;
using System;
using System.Linq;
using System.Text;

namespace Serein.Library
{

    /// <summary>
    /// 每个节点有独自的MethodDetails实例
    /// </summary>
    [NodeProperty(ValuePath = NodeValuePath.Method)]
    public partial class MethodDetails
    {
        // private readonly IFlowEnvironment env;

        /// <summary>
        /// 对应的节点
        /// </summary>
        [PropertyInfo(IsProtection = true)]
        private NodeModelBase _nodeModel;

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
        /// 方法别名
        /// </summary>
        [PropertyInfo]
        private string _methodAnotherName;

        /// <summary>
        /// 参数描述
        /// </summary>
        [PropertyInfo]
        private ParameterDetails[] _parameterDetailss;

        /// <summary>
        /// <para>描述该方法是否存在可选参数</para>
        /// <para>-1表示不存在</para>
        /// <para>0表示第一个参数是可选参数</para>
        /// </summary>
        [PropertyInfo] 
        private int _isParamsArgIndex = -1;

        /// <summary>
        /// 出参类型
        /// </summary>
        [PropertyInfo]
        private Type _returnType;
    }


    public partial class MethodDetails
    {

        #region 新增可选参数
        /// <summary>
        /// 新增可选参数
        /// </summary>
        /// <param name="index"></param>
        public void AddParamsArg(int index = 0)
        {
            if (IsParamsArgIndex >= 0  // 方法是否包含可选参数
                && index >= 0  // 如果包含，则判断从哪个参数赋值
                && index >= IsParamsArgIndex // 需要判断是否为可选参数的部分
                && index < ParameterDetailss.Length) // 防止下标越界
            {
                var newPd = ParameterDetailss[index].CloneOfModel(this.NodeModel); // 复制出属于本身节点的参数描述
                newPd.Index = ParameterDetailss.Length; // 更新索引
                newPd.IsParams = true;
                ParameterDetailss = AddToArray(ParameterDetailss, newPd); // 新增
            }
        }
        /// <summary>
        /// 移除可选参数
        /// </summary>
        /// <param name="index"></param>
        public void RemoveParamsArg(int index = 0)
        {
            if (IsParamsArgIndex >= 0  // 方法是否包含可选参数
                && index >= 0  // 如果包含，则判断从哪个参数赋值
                && index >= IsParamsArgIndex // 需要判断是否为可选参数的部分
                && index < ParameterDetailss.Length) // 防止下标越界
            {
                //var newPd = ParameterDetailss[index].CloneOfModel(this.NodeModel); // 复制出属于本身节点的参数描述
                //newPd.Index = ParameterDetailss.Length; // 更新索引
                ParameterDetailss[index] = null; // 释放对象引用
                ParameterDetailss = RemoteToArray(ParameterDetailss, index); // 新增
            }
        }

        public static T[] AddToArray<T>(T[] original, T newObject)
        {
            // 创建一个新数组，比原数组大1
            T[] newArray = new T[original.Length + 1];

            // 复制原数组的元素
            for (int i = 0; i < original.Length; i++)
            {
                newArray[i] = original[i];
            }

            // 将新对象放在最后一位
            newArray[newArray.Length - 1] = newObject;

            return newArray;
        }
        public static T[] RemoteToArray<T>(T[] original, int index)
        {
            if(index == 0)
            {
                return new T[0];
            }
            // 创建一个新数组，比原数组小1
            T[] newArray = new T[original.Length - 1];

            for (int i = 0; i < index; i++)
            {
                newArray[i] = original[i];
            }
            for (int i = index; i < newArray.Length; i++)
            {
                newArray[i] = original[i+1];
            }
            return newArray;
        }


        #endregion


        /// <summary>
        /// 不包含方法信息的基础节点（后续可能要改为DLL引入基础节点）
        /// </summary>
        public MethodDetails()
        {
            
        }
        /// <summary>
        /// 生成元数据
        /// </summary>
        /// <param name="nodeModel">标识属于哪个节点</param>
        public MethodDetails(NodeModelBase nodeModel)
        {
            NodeModel = nodeModel;
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
            MethodAnotherName = Info.MethodAnotherName;
            MethodDynamicType = nodeType;
            ReturnType = Type.GetType(Info.ReturnTypeFullName);
            ParameterDetailss = Info.ParameterDetailsInfos.Select(pinfo => new ParameterDetails(pinfo)).ToArray();
            IsParamsArgIndex = Info.IsParamsArgIndex;
        }

        /// <summary>
        /// 转为信息
        /// </summary>
        /// <returns></returns>
        public MethodDetailsInfo ToInfo()
        {
            return new MethodDetailsInfo
            {
                MethodName = this.MethodName,
                MethodAnotherName = this.MethodAnotherName,
                NodeType = this.MethodDynamicType.ToString(),
                ParameterDetailsInfos = this.ParameterDetailss.Select(p => p.ToInfo()).ToArray(),
                ReturnTypeFullName = this.ReturnType.FullName,
                IsParamsArgIndex = this.IsParamsArgIndex,
            };
        }

        /// <summary>
        /// 从DLL拖动出来时，从元数据拷贝新的实例，作为属于节点独享的方法描述
        /// </summary>
        /// <returns></returns>
        public MethodDetails CloneOfNode( NodeModelBase nodeModel)
        {
            // this => 是元数据
            var md = new MethodDetails( nodeModel) // 创建新节点时拷贝实例
            {
                ActingInstance = this.ActingInstance,
                ActingInstanceType = this.ActingInstanceType,
                MethodDynamicType = this.MethodDynamicType,
                MethodAnotherName = this.MethodAnotherName,
                ReturnType = this.ReturnType,
                MethodName = this.MethodName,
                MethodLockName = this.MethodLockName,
                IsProtectionParameter = this.IsProtectionParameter,
                IsParamsArgIndex= this.IsParamsArgIndex,
            };
            md.ParameterDetailss = this.ParameterDetailss?.Select(p => p?.CloneOfModel(nodeModel)).ToArray(); // 拷贝属于节点方法的新入参描述
            return md;
        }

        public override string ToString()
        {
             StringBuilder sb = new StringBuilder();
            sb.AppendLine($"方法别名：{this.MethodAnotherName}");
            sb.AppendLine($"方法名称：{this.MethodName}");
            sb.AppendLine($"需要实例：{this.ActingInstanceType?.FullName}");
            sb.AppendLine($"");
            sb.AppendLine($"入参参数信息：");
            for (int i = 0; i < ParameterDetailss.Length; i++)
            {
                ParameterDetails arg = this.ParameterDetailss[i];
            }
            sb.AppendLine($"");
            sb.AppendLine($"返回值信息：");
            sb.AppendLine($"    {this.ReturnType?.FullName}");
            return sb.ToString();
        }

    }

}
