using Serein.Library.Api;
using Serein.Library;
using System.Security.AccessControl;

namespace Serein.NodeFlow.Model
{
    /// <summary>
    /// 单动作节点（用于动作控件)
    /// </summary>
    public class SingleActionNode : NodeModelBase
    {
        public SingleActionNode(IFlowEnvironment environment):base(environment)
        {
            
        }
        public override ParameterData[] GetParameterdatas()
        {
            if (base.MethodDetails.ParameterDetailss.Length > 0)
            {
                return MethodDetails.ParameterDetailss
                                    .Select(it => new ParameterData
                                    {
                                        SourceNodeGuid = it.ArgDataSourceNodeGuid,
                                        SourceType = it.ArgDataSourceType.ToString(),
                                        State = it.IsExplicitData,
                                        Value = it.DataValue,
                                    })
                                    .ToArray();
            }
            else
            {
                return [];
            }
        }
    }


}
