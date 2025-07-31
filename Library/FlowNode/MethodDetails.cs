using Serein.Library.Api;
using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Serein.Library
{

    /// <summary>
    /// 每个节点有独自的MethodDetails实例
    /// </summary>
    [FlowDataProperty(ValuePath = NodeValuePath.Method)]
    public partial class MethodDetails
    {
        // private readonly IFlowEnvironment env;

        /// <summary>
        /// 对应的节点
        /// </summary>
        [DataInfo(IsProtection = true)]
        private IFlowNode _nodeModel;

        /// <summary>
        /// 对应的程序集
        /// </summary>
        [DataInfo]
        private string _assemblyName;


        /// <summary>
        /// 调用节点方法时需要的实例（多个相同的节点将拥有相同的类型）
        /// </summary>
        [DataInfo]
        private Type _actingInstanceType;

        /// <summary>
        /// 方法名称
        /// </summary>
        [DataInfo]
        private string _methodName;

        /// <summary>
        /// 节点类型
        /// </summary>
        [DataInfo]
        private NodeType _methodDynamicType;

        /// <summary>
        /// 锁名称（暂未实现）
        /// </summary>
        [DataInfo]
        private string _methodLockName;


        /// <summary>
        /// 方法别名
        /// </summary>
        [DataInfo]
        private string _methodAnotherName;

        /// <summary>
        /// 参数描述
        /// </summary>
        [DataInfo]
        private ParameterDetails[] _parameterDetailss;

        /// <summary>
        /// <para>描述该方法是否存在可选参数</para>
        /// <para>-1表示不存在</para>
        /// <para>0表示第一个参数是可选参数</para>
        /// </summary>
        [DataInfo] 
        private int _paramsArgIndex = -1;

        /// <summary>
        /// 是否为异步方法（如果为异步方法，则返回值类型为Task或Task&lt;T&gt;）
        /// </summary>
        [DataInfo]
        private bool _isAsync = false;

        /// <summary>
        /// 出参类型
        /// </summary>
        [DataInfo]
        private Type _returnType;
    }


    public partial class MethodDetails
    {

        #region 更改可变参数
        /// <summary>
        /// 是否存在可变参数（-1表示不存在）
        /// </summary>
        public bool HasParamsArg => _paramsArgIndex >= 0;

        /// <summary>
        /// 是否为静态方法
        /// </summary>
        public bool IsStatic { get;  set; }

        /// <summary>
        /// 新增可变参数
        /// </summary>
        /// <param name="index"></param>
        public bool AddParamsArg(int index)
        {
            if (ParamsArgIndex < 0  // 方法是否包含可变参数
                || index < 0  // 如果包含，则判断从哪个参数赋值
                || index < ParamsArgIndex // 需要判断是否为可选参数的部分
                || index >= ParameterDetailss.Length) // 防止下标越界
            {
                return false;
            }


            var newPd = ParameterDetailss[index].CloneOfModel(this.NodeModel); // 复制出属于本身节点的参数描述
            newPd.Index = ParameterDetailss.Length; // 更新索引
            newPd.IsParams = true;
            ParameterDetailss = ArrayHelper.AddToArray(ParameterDetailss, newPd); // 新增
            

            return true;
        }

        /// <summary>
        /// 新增可变参数
        /// </summary>
        /// <param name="parameterDetails"></param>
        /// <returns></returns>
        public bool AddParamsArg(ParameterDetails parameterDetails)
        {
            if (ParamsArgIndex < 0)  // 方法是否包含可变参数
            { 
                return false;
            }
            if (parameterDetails is null)
            {
                return false;
            }
            parameterDetails.Index = ParameterDetailss.Length; // 更新索引
            parameterDetails.IsParams = true;
            ParameterDetailss = ArrayHelper.AddToArray(ParameterDetailss, parameterDetails); // 新增

            return true;
            
        }


        /// <summary>
        /// 移除可变参数
        /// </summary>
        /// <param name="index"></param>
        public bool RemoveParamsArg(int index)
        {

            if (ParamsArgIndex < 0  // 方法是否包含可变参数
                || index < 0  // 如果包含，则判断从哪个参数赋值
                || index <= ParamsArgIndex // 需要判断是否为可选参数的部分，并且不能删除原始的可变参数描述
                || index >= ParameterDetailss.Length) // 防止下标越界
            {
                return false;
            }

            ParameterDetailss[index] = null; // 释放对象引用
            var tmp = ArrayHelper.RemoteToArray<ParameterDetails>(ParameterDetailss, index); // 新增;
            UpdateParamIndex(ref tmp);
            ParameterDetailss = tmp; // 新增
            return true;
        }

        /// <summary>
        /// 移除可变参数
        /// </summary>
        /// <param name="parameterDetails"></param>
        public bool RemoveParamsArg(ParameterDetails parameterDetails)
        {
            if (ParamsArgIndex < 0)  // 方法是否包含可变参数
            {
                return false;
            }
            if (parameterDetails is null)
            {
                return false;
            }
            int index = -1;
            for (int i = 0; i < ParameterDetailss.Length; i++)
            {
                var pd = ParameterDetailss[i];
                if (pd.Equals(parameterDetails))
                {
                    index = i;
                    break;
                }
            }
            if (index == -1)
            {
                return false;
            }

            ParameterDetailss[index] = null; // 释放对象引用
            var tmp = ArrayHelper.RemoteToArray<ParameterDetails>(ParameterDetailss, index); // 新增;
            UpdateParamIndex(ref tmp);
            ParameterDetailss = tmp; // 新增
            return true;
        }


        /// <summary>
        /// 更新参数的索引
        /// </summary>
        /// <param name="parameterDetails"></param>
        private void UpdateParamIndex(ref ParameterDetails[] parameterDetails)
        {
            for (int i = 0; i < parameterDetails.Length; i++)
            {
                var pd = parameterDetails[i];
                pd.Index = i;
            }
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
        public MethodDetails(IFlowNode nodeModel)
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
            AssemblyName = Info.AssemblyName;
            MethodName = Info.MethodName;
            MethodAnotherName = Info.MethodAnotherName;
            MethodDynamicType = nodeType;
            ReturnType = Type.GetType(Info.ReturnTypeFullName);
            ParameterDetailss = Info.ParameterDetailsInfos.Select(pinfo => new ParameterDetails(pinfo)).ToArray();
            ParamsArgIndex = Info.IsParamsArgIndex;
        }

        /// <summary>
        /// 转为信息
        /// </summary>
        /// <returns></returns>
        public MethodDetailsInfo ToInfo()
        {
            return new MethodDetailsInfo
            {
                AssemblyName = this.AssemblyName,
                MethodName = this.MethodName,
                MethodAnotherName = this.MethodAnotherName,
                NodeType = this.MethodDynamicType.ToString(),
                ParameterDetailsInfos = this.ParameterDetailss?.Select(p => p.ToInfo()).ToArray(),
                ReturnTypeFullName = this.ReturnType?.FullName,
                IsParamsArgIndex = this.ParamsArgIndex,
                IsAsync = this.IsAsync,
            };
        }

        /// <summary>
        /// 从DLL拖动出来时，从元数据拷贝新的实例，作为属于节点独享的方法描述
        /// </summary>
        /// <returns></returns>
        public MethodDetails CloneOfNode(IFlowNode nodeModel)
        {
            // this => 是元数据
            var md = new MethodDetails(nodeModel) // 创建新节点时拷贝实例
            {
                AssemblyName = this.AssemblyName, // 拷贝
                //ActingInstance = this.ActingInstance,
                ActingInstanceType = this.ActingInstanceType,  // 拷贝
                MethodDynamicType = this.MethodDynamicType,  // 拷贝
                MethodAnotherName = this.MethodAnotherName,  // 拷贝
                ReturnType = this.ReturnType,  // 拷贝
                MethodName = this.MethodName,  // 拷贝
                MethodLockName = this.MethodLockName,  // 拷贝
                ParamsArgIndex = this.ParamsArgIndex,  // 拷贝
                ParameterDetailss = this.ParameterDetailss?.Select(p => p?.CloneOfModel(nodeModel)).ToArray() , // 拷贝属于节点方法的新入参描述
                IsAsync = this.IsAsync, // 拷贝
                IsStatic = this.IsStatic, // 拷贝
            };

            return md;
        }

        /// <summary>
        /// 将方法信息转换为字符串，方便调试和查看
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (string.IsNullOrEmpty(this.MethodName))
            {
                return "";
            }
            var tmp = this.MethodName.Split('.') ;
            var methodName = tmp[tmp.Length - 1];
             StringBuilder sb = new StringBuilder();
            sb.AppendLine($"节点Guid：");
            sb.AppendLine($"\t{this.NodeModel.Guid}");
            sb.AppendLine();
            sb.AppendLine($"方法名称：");
            sb.AppendLine($"\t{methodName}");
            sb.AppendLine($"方法别名：");
            sb.AppendLine($"\t{this.MethodAnotherName}");
            sb.AppendLine($"需要实例：");
            sb.AppendLine($"\t{this.ActingInstanceType?.FullName}");
            sb.AppendLine($"");
            sb.AppendLine($"入参信息：");
            for (int i = 0; i < ParameterDetailss.Length; i++)
            {
                ParameterDetails arg = this.ParameterDetailss[i];
                sb.AppendLine(arg.ToString());
            }
            sb.AppendLine();
            sb.AppendLine($"返回值信息：");
            sb.AppendLine($"\t{this.ReturnType?.GetFriendlyName()}");
            return sb.ToString();
        }

    }

}
