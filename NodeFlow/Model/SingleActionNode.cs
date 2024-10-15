using Serein.Library.Api;
using Serein.Library.Entity;
using Serein.NodeFlow.Base;

namespace Serein.NodeFlow.Model
{
    /// <summary>
    /// 单动作节点（用于动作控件)
    /// </summary>
    public class SingleActionNode : NodeModelBase
    {

        public override Parameterdata[] GetParameterdatas()
        {
            if (base.MethodDetails.ParameterDetailss.Length > 0)
            {
                return MethodDetails.ParameterDetailss
                                    .Select(it => new Parameterdata
                                    {
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
