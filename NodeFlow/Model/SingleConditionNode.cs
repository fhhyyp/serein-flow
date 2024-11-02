using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.Library.Utils.SereinExpression;
using System.ComponentModel;

namespace Serein.NodeFlow.Model
{
    /// <summary>
    /// 条件节点（用于条件控件）
    /// </summary>
    [NodeProperty(ValuePath = NodeValuePath.Node)]
    public partial class SingleConditionNode : NodeModelBase
    {

        /// <summary>
        /// 是否为自定义参数
        /// </summary>
        [PropertyInfo(IsNotification = true)]
        private bool _isCustomData;

        /// <summary>
        /// 自定义参数值
        /// </summary>
        [PropertyInfo(IsNotification = true)]
        private string? _customData;

        /// <summary>
        /// 条件表达式
        /// </summary>
        [PropertyInfo(IsNotification = true)]
        private string _expression;
    }

    public partial class SingleConditionNode : NodeModelBase
    {
        public SingleConditionNode(IFlowEnvironment environment):base(environment)
        {
            this.IsCustomData = false;
            this.CustomData = null;
            this.Expression = "PASS";
        }

       


        /// <summary>
        /// 重写节点的方法执行
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override async Task<object?> ExecutingAsync(IDynamicContext context)
        {
            // 接收上一节点参数or自定义参数内容
            object? parameter;
            object? result = null;
            if (!IsCustomData) // 是否使用自定义参数
            {

                var pd = MethodDetails.ParameterDetailss[0];

                if (pd.ArgDataSourceType == ConnectionArgSourceType.GetOtherNodeData) 
                {
                    // 使用自定义节点的参数
                    result = context.GetFlowData(pd.ArgDataSourceNodeGuid);
                }
                else if (pd.ArgDataSourceType == ConnectionArgSourceType.GetOtherNodeDataOfInvoke)
                {
                    // 立刻调用目标节点，然后使用其返回值
                    result = await Env.InvokeNodeAsync(context, pd.ArgDataSourceNodeGuid);
                }
                else
                {
                    // 条件节点透传上一节点的数据
                    result = context.TransmissionData(this);   
                }
                
                // 使用上一节点的参数
                parameter = result;

            }
            else
            {
                
                var getObjExp = CustomData?.ToString();
                if (string.IsNullOrEmpty(getObjExp) || getObjExp.Length < 4 || !getObjExp[..4].Equals("@get", StringComparison.CurrentCultureIgnoreCase))
                {
                    // 使用自定义的参数
                    parameter = CustomData;
                }
                else
                {
                    // 表达式获取上一节点数据
                    parameter = result;
                    if (parameter is not null)
                    {
                        parameter = SerinExpressionEvaluator.Evaluate(getObjExp, parameter, out _);
                    }
                }
            }

            try
            {
                
                var isPass = SereinConditionParser.To(parameter, Expression);
                context.NextOrientation = isPass ? ConnectionInvokeType.IsSucceed : ConnectionInvokeType.IsFail;
            }
            catch (Exception ex)
            {
                context.NextOrientation = ConnectionInvokeType.IsError;
                RuningException = ex;
            }
            
            Console.WriteLine($"{result} {Expression}  -> " + context.NextOrientation);
            return result;
        }



        public override ParameterData[] GetParameterdatas()
        {
            var pd1 = MethodDetails.ParameterDetailss[0];
            var pd2 = MethodDetails.ParameterDetailss[1];
            var pd3 = MethodDetails.ParameterDetailss[2];
            return [
                new ParameterData // 保存表达式
                {
                    Value =  Expression ,
                    SourceNodeGuid = pd1.ArgDataSourceNodeGuid,
                    SourceType = pd1.ArgDataSourceType.ToString(),
                }, 
                new ParameterData // 保存自定义参数
                {
                    Value =  CustomData?.ToString() ,
                    SourceNodeGuid = pd2.ArgDataSourceNodeGuid,
                    SourceType = pd2.ArgDataSourceType.ToString(),
                },
                new ParameterData // 参数来源状态
                {
                    Value =  IsCustomData.ToString() ,
                    SourceNodeGuid = pd3.ArgDataSourceNodeGuid,
                    SourceType = pd3.ArgDataSourceType.ToString(),
                }];
        }

        public override void OnCreating()
        {
            // 自定义节点初始化默认的参数实体
            var tmpParameterDetails = new ParameterDetails[3];
            for (int index = 0; index <= 2; index++)
            {
                tmpParameterDetails[index] = new ParameterDetails
                {
                    Index = index,
                    IsExplicitData = false,
                    DataValue = string.Empty,
                    ArgDataSourceNodeGuid = string.Empty,
                    ArgDataSourceType = ConnectionArgSourceType.GetPreviousNodeData,
                    NodeModel = this,
                    Convertor = null,
                    ExplicitTypeName = "Value",
                    Items = Array.Empty<string>(),
                };
            }

            var pd1 = tmpParameterDetails[0]; // 表达式
            var pd2 = tmpParameterDetails[1]; // 自定义参数
            var pd3 = tmpParameterDetails[2]; // 参数来源

            // 表达式
            pd1.Name = nameof(Expression);
            pd1.DataType = typeof(string);
            pd1.ExplicitType = typeof(string);

            // 自定义参数
            pd2.Name = nameof(CustomData);
            pd2.DataType = typeof(string);
            pd2.ExplicitType = typeof(string);

            // 参数来源
            pd3.Name = nameof(IsCustomData);
            pd3.DataType = typeof(bool);
            pd3.ExplicitType = typeof(bool);

            //this.MethodDetails.ParameterDetailss = new ParameterDetails[2] { pd1, pd2 };
            this.MethodDetails.ParameterDetailss = [..tmpParameterDetails];
        }

  


        public override NodeModelBase LoadInfo(NodeInfo nodeInfo)
        {
            this.Guid = nodeInfo.Guid;
            this.Position = nodeInfo.Position;// 加载位置信息

            var pdInfo1 = nodeInfo.ParameterData[0];
            this.Expression = pdInfo1.Value; // 加载表达式
            
            var pdInfo2 = nodeInfo.ParameterData[1];
            this.CustomData = pdInfo2.Value; // 加载自定义参数信息

            var pdInfo3 = nodeInfo.ParameterData[2];
            bool.TryParse(pdInfo3.Value,out var @bool); // 参数来源状态
            this.IsCustomData = @bool;

            for (int i = 0; i < nodeInfo.ParameterData.Length; i++)
            {
                var pd = this.MethodDetails.ParameterDetailss[i]; // 本节点的参数信息
                ParameterData? pdInfo = nodeInfo.ParameterData[i]; // 项目文件的保存信息
                
                pd.ArgDataSourceNodeGuid = pdInfo.SourceNodeGuid;
                pd.ArgDataSourceType = EnumHelper.ConvertEnum<ConnectionArgSourceType>(pdInfo.SourceType);
            }
            return this;
        }

       
    }


}
