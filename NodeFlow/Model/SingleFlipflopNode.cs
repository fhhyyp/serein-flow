using Serein.Library.Api;
using Serein.Library.Entity;
using Serein.Library.Ex;
using Serein.NodeFlow.Base;

namespace Serein.NodeFlow.Model
{

    public class SingleFlipflopNode : NodeModelBase
    {
        public override object? Execute(IDynamicContext context)
        {
            NextOrientation = Library.Enums.ConnectionType.IsError;
            RuningException = new FlipflopException ("无法以非await/async的形式调用触发器");
            return null;
        }

        public override Parameterdata[] GetParameterdatas()
        {
            if (base.MethodDetails.ExplicitDatas.Length > 0)
            {
                return MethodDetails.ExplicitDatas
                                     .Select(it => new Parameterdata
                                     {
                                         state = it.IsExplicitData,
                                         value = it.DataValue
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
